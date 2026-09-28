// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Stores;

/// <summary>
/// A store that lives as long as the process. For tests, and for a console
/// tool that signs in on every run; an app that should survive a restart wants
/// a store that persists — on MAUI, Freedom.Client.Maui's SecureStorageFreedomStore.
/// </summary>
public sealed class InMemoryFreedomStore : IFreedomStore
{
	private readonly Lock _lock = new();
	private FreedomSnapshot _snapshot = FreedomSnapshot.Empty;

	/// <inheritdoc/>
	public Task<FreedomSnapshot> LoadAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			return Task.FromResult(_snapshot);
		}
	}

	/// <inheritdoc/>
	public Task ApplyAsync(FreedomChangeSet changes, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(changes);

		lock (_lock)
		{
			_snapshot = Apply(_snapshot, changes);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task MarkVerifiedAsync(string etag, DateTimeOffset at, CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			_snapshot = _snapshot with { Etag = etag, VerifiedAt = at };
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			_snapshot = FreedomSnapshot.Empty;
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// A snapshot with a change set applied. Shared by every store in this
	/// library, so they agree on what a change set means.
	/// </summary>
	public static FreedomSnapshot Apply(FreedomSnapshot snapshot, FreedomChangeSet changes)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentNullException.ThrowIfNull(changes);

		var values = new Dictionary<string, FreedomValue>(snapshot.Values, StringComparer.Ordinal);

		foreach (var key in changes.Removals)
		{
			values.Remove(key);
		}

		foreach (var value in changes.Upserts)
		{
			values[value.Key] = value;
		}

		return new FreedomSnapshot(values, changes.Etag, changes.VerifiedAt);
	}
}
