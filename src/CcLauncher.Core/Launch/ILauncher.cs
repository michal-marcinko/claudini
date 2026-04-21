namespace CcLauncher.Core.Launch;

public interface ILauncher
{
    LaunchResult Launch(LaunchRequest request);
}
