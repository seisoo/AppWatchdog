public sealed class PipeProtocolMismatchException : InvalidOperationException
{
    public PipeProtocolMismatchException(int uiVersion, int serviceVersion)
        : base(
            $"Protocol mismatch. UI={uiVersion}, Service={serviceVersion}. " +
            "Bitte Dienst neu starten oder aktualisieren.")
    {
        UiVersion = uiVersion;
        ServiceVersion = serviceVersion;
    }

    public int UiVersion { get; }
    public int ServiceVersion { get; }
}