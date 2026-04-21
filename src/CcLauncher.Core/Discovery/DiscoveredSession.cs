// src/CcLauncher.Core/Discovery/DiscoveredSession.cs
namespace CcLauncher.Core.Discovery;

public sealed record DiscoveredSession(
    string Id,
    string FilePath,
    DateTime StartedAt,
    DateTime LastActivity,
    int MessageCount,
    string? FirstUserMsg);
