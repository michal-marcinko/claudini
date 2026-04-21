using System;
using CcLauncher.Core.Discovery;

namespace CcLauncher.App.ViewModels;

public sealed class SessionRowViewModel
{
    public SessionRowViewModel(DiscoveredSession session) => Session = session;
    public DiscoveredSession Session { get; }

    public string Id            => Session.Id;
    public DateTime StartedAt   => Session.StartedAt;
    public DateTime LastActivity => Session.LastActivity;
    public int MessageCount     => Session.MessageCount;
    public string DisplayText   => Session.FirstUserMsg ?? "(no preview)";
    public string RelativeWhen  => RelativeTime.Format(Session.LastActivity);
}

internal static class RelativeTime
{
    public static string Format(DateTime utc)
    {
        var delta = DateTime.UtcNow - utc;
        if (delta.TotalMinutes < 1)  return "just now";
        if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalHours   < 24) return $"{(int)delta.TotalHours}h ago";
        if (delta.TotalDays    < 30) return $"{(int)delta.TotalDays}d ago";
        return utc.ToLocalTime().ToString("yyyy-MM-dd");
    }
}
