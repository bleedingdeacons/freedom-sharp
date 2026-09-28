using TheBleedingDeacons.Freedom.Client.Abstractions;
using TheBleedingDeacons.Freedom.Client.Serialization;
using TheBleedingDeacons.Freedom.Client.Stores;

namespace FreedomCli;

/// <summary>
/// Configuration and credentials in two JSON files, so a second run can show
/// "nothing changed". <b>For the example only</b>: the files are plain text,
/// secrets and private key included. A real app keeps them in platform secure
/// storage — on MAUI, Freedom.Client.Maui's stores.
/// </summary>
internal sealed class FileStores(string directory) : IFreedomStore, IFreedomCredentialStore
{
	private readonly string _config = Path.Combine(directory, "config.json");
	private readonly string _credentials = Path.Combine(directory, "credentials.json");

	public async Task<FreedomSnapshot> LoadAsync(CancellationToken cancellationToken) =>
		File.Exists(_config) ? FreedomStateJson.DeserializeSnapshot(await File.ReadAllTextAsync(_config, cancellationToken)) : FreedomSnapshot.Empty;

	public async Task ApplyAsync(FreedomChangeSet changes, CancellationToken cancellationToken) =>
		await WriteAsync(InMemoryFreedomStore.Apply(await LoadAsync(cancellationToken), changes), cancellationToken);

	public async Task MarkVerifiedAsync(string etag, DateTimeOffset at, CancellationToken cancellationToken) =>
		await WriteAsync(await LoadAsync(cancellationToken) with { Etag = etag, VerifiedAt = at }, cancellationToken);

	Task IFreedomStore.ClearAsync(CancellationToken cancellationToken)
	{
		File.Delete(_config);
		return Task.CompletedTask;
	}

	async Task<TabletCredentials?> IFreedomCredentialStore.LoadAsync(CancellationToken cancellationToken) =>
		File.Exists(_credentials) ? FreedomStateJson.DeserializeCredentials(await File.ReadAllTextAsync(_credentials, cancellationToken)) : null;

	public async Task SaveAsync(TabletCredentials credentials, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(directory);
		await File.WriteAllTextAsync(_credentials, FreedomStateJson.Serialize(credentials), cancellationToken);
	}

	Task IFreedomCredentialStore.ClearAsync(CancellationToken cancellationToken)
	{
		File.Delete(_credentials);
		return Task.CompletedTask;
	}

	private async Task WriteAsync(FreedomSnapshot snapshot, CancellationToken cancellationToken)
	{
		Directory.CreateDirectory(directory);
		await File.WriteAllTextAsync(_config, FreedomStateJson.Serialize(snapshot), cancellationToken);
	}
}
