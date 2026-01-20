using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppWatchdog.Shared;

public static class PipeClient
{
    public static TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(1.5);
    public static TimeSpan RoundtripTimeout { get; set; } = TimeSpan.FromSeconds(3);

    public static WatchdogConfig GetConfig()
        => SendRequestNoPayload<WatchdogConfig>(PipeProtocol.CmdGetConfig);

    public static ServiceSnapshot GetStatus()
        => SendRequestNoPayload<ServiceSnapshot>(PipeProtocol.CmdGetStatus);

    public static void SaveConfig(WatchdogConfig cfg)
        => SendRequest(PipeProtocol.CmdSaveConfig, cfg);

    public static void TriggerCheck()
        => SendRequestNoPayload<object?>(PipeProtocol.CmdTriggerCheck);

    public static void Ping()
        => SendRequestNoPayload<object?>(PipeProtocol.CmdPing);

    public static LogDaysResponse ListLogDays()
        => SendRequestNoPayload<LogDaysResponse>(PipeProtocol.CmdListLogDays);

    public static LogPathResponse GetLogPath()
        => SendRequestNoPayload<LogPathResponse>(PipeProtocol.CmdGetLogPath);

    public static LogDayResponse GetLogDay(string day)
        => SendRequestWithPayload<LogDayResponse>(PipeProtocol.CmdGetLogDay, new LogDayRequest { Day = day });

    private static void SendRequest<TPayload>(string command, TPayload payload)
    {
        var req = new PipeProtocol.Request
        {
            Version = PipeProtocol.ProtocolVersion,
            Command = command,
            PayloadJson = PipeProtocol.Serialize(payload)
        };

        var resp = Roundtrip(req);

        ValidateResponse(resp);
    }

    private static T SendRequestNoPayload<T>(string command)
    {
        var req = new PipeProtocol.Request
        {
            Version = PipeProtocol.ProtocolVersion,
            Command = command
        };

        var resp = Roundtrip(req);

        ValidateResponse(resp);

        if (typeof(T) == typeof(object))
            return default!;

        if (string.IsNullOrWhiteSpace(resp.PayloadJson))
            throw new InvalidOperationException("Service returned no payload.");

        var obj = PipeProtocol.Deserialize<T>(resp.PayloadJson);
        if (obj == null)
            throw new InvalidOperationException("Invalid payload from service.");

        return obj;
    }

    public static T SendRequestWithPayload<T>(string command, object payload)
    {
        var req = new PipeProtocol.Request
        {
            Version = PipeProtocol.ProtocolVersion,
            Command = command,
            PayloadJson = PipeProtocol.Serialize(payload)
        };

        var resp = Roundtrip(req);
        ValidateResponse(resp);

        if (string.IsNullOrWhiteSpace(resp.PayloadJson))
            throw new InvalidOperationException("Service returned no payload.");

        var obj = PipeProtocol.Deserialize<T>(resp.PayloadJson)!;
        if (obj == null)
            throw new InvalidOperationException("Invalid payload from service.");

        return obj;
    }

    private static void ValidateResponse(PipeProtocol.Response resp)
    {
        if (resp.Version != PipeProtocol.ProtocolVersion)
        {
            throw new PipeProtocolMismatchException(
                PipeProtocol.ProtocolVersion,
                resp.Version);
        }

        if (!resp.Ok)
            throw new InvalidOperationException(resp.Error ?? "Service returned error.");
    }

    private static PipeProtocol.Response Roundtrip(PipeProtocol.Request req)
    {
        using var client = new NamedPipeClientStream(
            ".",
            PipeProtocol.PipeName,
            PipeDirection.InOut,
            PipeOptions.None);

        client.Connect((int)ConnectTimeout.TotalMilliseconds);

        var reqJson = PipeProtocol.Serialize(req);
        var reqBytes = Encoding.UTF8.GetBytes(reqJson);

        using (var bw = new BinaryWriter(client, Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(reqBytes.Length);
            bw.Write(reqBytes);
            bw.Flush();
        }

        var readTask = Task.Run(() =>
        {
            using var br = new BinaryReader(client, Encoding.UTF8, leaveOpen: true);

            int len = br.ReadInt32();
            if (len <= 0 || len > 1024 * 1024)
                throw new InvalidOperationException("Ungültige Antwortlänge.");

            var bytes = br.ReadBytes(len);
            if (bytes.Length != len)
                throw new InvalidOperationException("Unvollständige Antwort.");

            var json = Encoding.UTF8.GetString(bytes);
            return PipeProtocol.Deserialize<PipeProtocol.Response>(json)
                   ?? throw new InvalidOperationException("Ungültige Antwort.");
        });

        if (!readTask.Wait(RoundtripTimeout))
            throw new TimeoutException("Service antwortet nicht (Pipe Timeout).");

        return readTask.Result;
    }

    public static void TestSmtp()
        => SendRequestNoPayload<object?>(PipeProtocol.CmdTestSmtp);

    public static void TestNtfy()
        => SendRequestNoPayload<object?>(PipeProtocol.CmdTestNtfy);

}
