using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace FreedomCli;

/// <summary>The console as a device: identified by whatever FREEDOM_DEVICE_ID says.</summary>
internal sealed class CliDevice(string deviceId) : IDeviceIdentity
{
	public Task<DeviceIdentity> GetAsync(CancellationToken cancellationToken) =>
		Task.FromResult(new DeviceIdentity(deviceId, "cli", Environment.MachineName, "console", "0.1.0"));
}
