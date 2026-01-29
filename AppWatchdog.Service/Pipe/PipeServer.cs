using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AppWatchdog.Service.Pipe;

/// <summary>
/// Hosts the named pipe server used for service communication.
/// </summary>
public sealed class PipeServer
{
    private readonly Func<PipeProtocol.Request, PipeProtocol.Response> _handler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipeServer"/> class.
    /// </summary>
    /// <param name="handler">Request handler for pipe messages.</param>
    public PipeServer(Func<PipeProtocol.Request, PipeProtocol.Response> handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Runs the pipe accept loop until cancellation is requested.
    /// </summary>
    /// <param name="token">Token used to stop the loop.</param>
    /// <returns>A task that completes when the loop stops.</returns>
    public async Task RunAsync(CancellationToken token)
    {
        FileLogStore.WriteLine("INFO", "PipeServer AcceptLoop gestartet");

        while (!token.IsCancellationRequested)
        {
            var pipe = CreatePipe();

            try
            {
                await pipe.WaitForConnectionAsync(token);

                _ = Task.Run(
                    () => PipeClientHandler.HandleAsync(pipe, _handler),
                    token);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (Exception ex)
            {
                pipe.Dispose();
                FileLogStore.WriteLine(
                    "ERROR",
                    $"Pipe Accept Fehler: {ex}");
                await Task.Delay(500, token);
            }
        }
    }

    /// <summary>
    /// Creates a secured named pipe server stream.
    /// </summary>
    /// <returns>The configured named pipe server stream.</returns>
    private static NamedPipeServerStream CreatePipe()
    {
        var ps = new PipeSecurity();

        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        ps.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            PipeProtocol.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous,
            0, 0,
            ps);
    }
}
