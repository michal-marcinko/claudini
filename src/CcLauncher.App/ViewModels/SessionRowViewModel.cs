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
    // Match Claude Code's resume picker: it labels sessions with the most recent
    // prompt, not the first. Slug is a three-word codename — useful as a last
    // resort before "(no preview)" when the session has no prompt content yet.
    public string DisplayText =>
        NonBlank(Session.LastPrompt) ??
        NonBlank(Session.FirstUserMsg) ??
        NonBlank(Session.Slug) ??
        "(no preview)";

    private static string? NonBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
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
