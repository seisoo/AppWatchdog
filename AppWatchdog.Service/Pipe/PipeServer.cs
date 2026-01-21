using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AppWatchdog.Service.Pipe;

public sealed class PipeServer
{
    private readonly ILogger _log;
    private readonly Func<PipeProtocol.Request, PipeProtocol.Response> _handler;

    public PipeServer(ILogger log, Func<PipeProtocol.Request, PipeProtocol.Response> handler)
    {
        _log = log;
        _handler = handler;
    }

    public async Task RunAsync(CancellationToken token)
    {
        FileLogStore.WriteLine("INFO","PipeServer AcceptLoop gestartet");

        while (!token.IsCancellationRequested)
        {
            var pipe = CreatePipe();

            try
            {
                await pipe.WaitForConnectionAsync(token);
                //FileLogStore.WriteLine("INFO","Pipe client connected"); //spammt den log unendlich lange

                _ = Task.Run(() => PipeClientHandler.HandleAsync(pipe, _handler, _log), token);
            }
            catch (OperationCanceledException)
            {
                pipe.Dispose();
                break;
            }
            catch (Exception ex)
            {
                pipe.Dispose();
                FileLogStore.WriteLine("ERROR", string.Format("Pipe Accept Fehler: {0}", ex));
                await Task.Delay(500, token);
            }
        }
    }

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
