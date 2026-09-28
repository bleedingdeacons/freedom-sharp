// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Stores;

/// <summary>
/// Credentials that live as long as the process. For tests and console tools.
/// </summary>
public sealed class InMemoryCredentialStore : IFreedomCredentialStore
{
	private TabletCredentials? _credentials;

	/// <inheritdoc/>
	public Task<TabletCredentials?> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Volatile.Read(ref _credentials));

	/// <inheritdoc/>
	public Task SaveAsync(TabletCredentials credentials, CancellationToken cancellationToken)
	{
		Volatile.Write(ref _credentials, credentials);
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken)
	{
		Volatile.Write(ref _credentials, null);
		return Task.CompletedTask;
	}
}
