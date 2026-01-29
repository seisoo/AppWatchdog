using System.ComponentModel;
using System.Runtime.InteropServices;
using AppWatchdog.Shared;

namespace AppWatchdog.Service.Helpers;

/// <summary>
/// Launches processes in the active interactive user session.
/// </summary>
public static class UserSessionLauncher
{
    /// <summary>
    /// Gets the current user session state.
    /// </summary>
    /// <returns>The user session state.</returns>
    public static UserSessionState GetSessionState()
    {
        uint sessionId = WTSGetActiveConsoleSessionId();
        return sessionId == 0xFFFFFFFF ? UserSessionState.NoInteractiveUser : UserSessionState.InteractiveUserPresent;
    }

    /// <summary>
    /// Starts a process in the active user session.
    /// </summary>
    /// <param name="exePath">Path to the executable.</param>
    /// <param name="arguments">Command-line arguments.</param>
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

    /// <summary>
    /// Win32 token access mask for process creation.
    /// </summary>
    private const int TOKEN_ALL_ACCESS = 0xF01FF;

    /// <summary>
    /// Creates a Unicode environment block for the process.
    /// </summary>
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    /// <summary>
    /// Token type values.
    /// </summary>
    private enum TOKEN_TYPE { TokenPrimary = 1 }

    /// <summary>
    /// Token impersonation level values.
    /// </summary>
    private enum SECURITY_IMPERSONATION_LEVEL { SecurityImpersonation = 2 }

    /// <summary>
    /// Win32 STARTUPINFO structure.
    /// </summary>
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

    /// <summary>
    /// Win32 PROCESS_INFORMATION structure.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess;
        public nint hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    /// <summary>
    /// Gets the active console session ID.
    /// </summary>
    /// <returns>The active session ID.</returns>
    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    /// <summary>
    /// Queries the user token for a session.
    /// </summary>
    /// <param name="SessionId">Session identifier.</param>
    /// <param name="phToken">Token handle.</param>
    /// <returns><c>true</c> if the token was obtained.</returns>
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint SessionId, out nint phToken);

    /// <summary>
    /// Duplicates a user token for process creation.
    /// </summary>
    /// <param name="hExistingToken">Existing token handle.</param>
    /// <param name="dwDesiredAccess">Desired access flags.</param>
    /// <param name="lpTokenAttributes">Token attributes pointer.</param>
    /// <param name="ImpersonationLevel">Impersonation level.</param>
    /// <param name="TokenType">Token type.</param>
    /// <param name="phNewToken">New token handle.</param>
    /// <returns><c>true</c> if duplication succeeded.</returns>
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        nint hExistingToken,
        int dwDesiredAccess,
        nint lpTokenAttributes,
        SECURITY_IMPERSONATION_LEVEL ImpersonationLevel,
        TOKEN_TYPE TokenType,
        out nint phNewToken);

    /// <summary>
    /// Creates a process as a specific user.
    /// </summary>
    /// <param name="hToken">User token handle.</param>
    /// <param name="lpApplicationName">Application name.</param>
    /// <param name="lpCommandLine">Command line.</param>
    /// <param name="lpProcessAttributes">Process attributes pointer.</param>
    /// <param name="lpThreadAttributes">Thread attributes pointer.</param>
    /// <param name="bInheritHandles">Whether handles are inheritable.</param>
    /// <param name="dwCreationFlags">Creation flags.</param>
    /// <param name="lpEnvironment">Environment block pointer.</param>
    /// <param name="lpCurrentDirectory">Current directory.</param>
    /// <param name="lpStartupInfo">Startup info.</param>
    /// <param name="lpProcessInformation">Process information.</param>
    /// <returns><c>true</c> if process creation succeeded.</returns>
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

    /// <summary>
    /// Closes a native handle.
    /// </summary>
    /// <param name="hObject">Handle to close.</param>
    /// <returns><c>true</c> if the handle was closed.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);
}
