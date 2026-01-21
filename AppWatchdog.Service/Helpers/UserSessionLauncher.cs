using System.ComponentModel;
using System.Runtime.InteropServices;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Helpers;

public static class UserSessionLauncher
{
    public static UserSessionState GetSessionState()
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        return sessionId == 0xFFFFFFFF ? UserSessionState.NoInteractiveUser : UserSessionState.InteractiveUserPresent;
    }

    public static void StartInActiveUserSession(string exePath, string arguments)
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        if (sessionId == 0xFFFFFFFF)
            throw new InvalidOperationException("Kein interaktiver Benutzer angemeldet.");

        if (!WTSQueryUserToken(sessionId, out var userToken))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "WTSQueryUserToken fehlgeschlagen.");

        try
        {
            if (!DuplicateTokenEx(
                    userToken,
                    TOKEN_ALL_ACCESS,
                    nint.Zero,
                    SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                    TOKEN_TYPE.TokenPrimary,
                    out var primaryToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx fehlgeschlagen.");
            }

            try
            {
                var si = new STARTUPINFO
                {
                    cb = Marshal.SizeOf<STARTUPINFO>(),
                    lpDesktop = @"winsta0\default"
                };

                string cmdLine = $"\"{exePath}\" {arguments ?? ""}".Trim();

                if (!CreateProcessAsUser(
                        primaryToken,
                        null,
                        cmdLine,
                        nint.Zero,
                        nint.Zero,
                        false,
                        CREATE_UNICODE_ENVIRONMENT,
                        nint.Zero,
                        Path.GetDirectoryName(exePath) ?? Environment.SystemDirectory,
                        ref si,
                        out var pi))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateProcessAsUser fehlgeschlagen.");
                }

                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
            }
            finally
            {
                CloseHandle(primaryToken);
            }
        }
        finally
        {
            CloseHandle(userToken);
        }
    }

    private const int TOKEN_ALL_ACCESS = 0xF01FF;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    private enum TOKEN_TYPE { TokenPrimary = 1 }
    private enum SECURITY_IMPERSONATION_LEVEL { SecurityImpersonation = 2 }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public nint lpReserved2;
        public nint hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint SessionId, out nint phToken);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        nint hExistingToken,
        int dwDesiredAccess,
        nint lpTokenAttributes,
        SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
        TOKEN_TYPE TokenType,
        out nint phNewToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        nint hToken,
        string? lpApplicationName,
        string lpCommandLine,
        nint lpProcessAttributes,
        nint lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        nint lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);
}
