using AppWatchdog.Shared;

namespace AppWatchdog.Service.Helpers;

public sealed class StatusTracker
{
    private string _lastMessage = "";
    private DateTimeOffset _lastMailSent = DateTimeOffset.MinValue;

    public ServiceSnapshot? LastSnapshot { get; set; }

    public bool ShouldSendMail(string currentMessage, TimeSpan maxInterval)
    {
        bool changed = !string.Equals(_lastMessage, currentMessage, StringComparison.Ordinal);
        bool interval = DateTimeOffset.Now - _lastMailSent >= maxInterval;

        return changed || interval;
    }

    public void Update(string currentMessage)
    {
        if (!string.Equals(_lastMessage, currentMessage, StringComparison.Ordinal) ||
            DateTimeOffset.Now - _lastMailSent > TimeSpan.FromSeconds(1))
        {
            _lastMessage = currentMessage;
            _lastMailSent = DateTimeOffset.Now;
        }
    }
}
