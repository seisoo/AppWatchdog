using AppWatchdog.Service.Helpers;
using AppWatchdog.Shared;
using System.IO.Pipes;
using System.Text;

namespace AppWatchdog.Service.Pipe;

public static class PipeClientHandler
{
    public static async Task HandleAsync(
        NamedPipeServerStream pipe,
        Func<PipeProtocol.Request, PipeProtocol.Response> handler)
    {
        try
        {
            using var br = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true);
            using var bw = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true);

            int reqLen = br.ReadInt32();
            if (reqLen <= 0 || reqLen > 1024 * 1024 * 4)
                throw new InvalidOperationException("Ungültige Request-Länge.");

            var reqBytes = br.ReadBytes(reqLen);
            if (reqBytes.Length != reqLen)
                throw new InvalidOperationException("Unvollständiger Request.");

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
