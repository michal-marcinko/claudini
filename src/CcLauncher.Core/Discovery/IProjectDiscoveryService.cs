// src/CcLauncher.Core/Discovery/IProjectDiscoveryService.cs
namespace CcLauncher.Core.Discovery;

public interface IProjectDiscoveryService
{
    IReadOnlyList<DiscoveredProject> Scan();
}
