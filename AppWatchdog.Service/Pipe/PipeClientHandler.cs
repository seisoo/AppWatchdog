using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using System.IO.Pipes;
using System.Text;

namespace AppWatchdog.Service.Pipe;

/// <summary>
/// Handles a single pipe client request/response roundtrip.
/// </summary>
public static class PipeClientHandler
{
    /// <summary>
    /// Reads a request from the pipe, invokes the handler, and writes the response.
    /// </summary>
    /// <param name="pipe">Connected pipe stream.</param>
    /// <param name="handler">Handler that processes requests.</param>
    /// <returns>A task that completes when processing finishes.</returns>
    public static async Task HandleAsync(
        NamedPipeServerStream pipe,
        Func<PipeProtocol.Request, PipeProtocol.Response> handler)
    {
        try
        {
            using var br = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
            using var bw = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);

            int reqLen = br.ReadInt32();
            // Match PipeClient limit: 50MB
            const int MaxRequestLength = 50 * 1024 * 1024;
            
            if (reqLen <= 0 || reqLen > MaxRequestLength)
                throw new InvalidOperationException($"Ungültige Request-Länge: {reqLen} bytes (Max: {MaxRequestLength} bytes).");

            var reqBytes = br.ReadBytes(reqLen);
            if (reqBytes.Length != reqLen)
                throw new InvalidOperationException($"Unvollständiger Request: {reqBytes.Length} von {reqLen} bytes gelesen.");

            var reqJson = Encoding.UTF8.GetString(reqBytes);
            var req = PipeProtocol.Deserialize<PipeProtocol.Request>(reqJson)
                      ?? throw new InvalidOperationException("Ungültiger Request.");

            var resp = handler(req);

            var respJson = PipeProtocol.Serialize(resp);
            var respBytes = Encoding.UTF8.GetBytes(respJson);

            bw.Write(respBytes.Length);
            bw.Write(respBytes);
            bw.Flush();
        }
        catch (Exception ex)
        {
            FileLogStore.WriteLine(
                "ERROR",
                $"PIPE: client handling failed: {ex}");
        }
        finally
        {
            pipe.Dispose();
        }

        await Task.CompletedTask;
    }
}
