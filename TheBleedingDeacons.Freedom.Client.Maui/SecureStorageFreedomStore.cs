// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Microsoft.Maui.Storage;
using TheBleedingDeacons.Freedom.Client.Abstractions;
using TheBleedingDeacons.Freedom.Client.Serialization;
using TheBleedingDeacons.Freedom.Client.Stores;

namespace TheBleedingDeacons.Freedom.Client.Maui;

/// <summary>
/// The whole configuration — secrets included — as one entry in platform
/// secure storage.
/// </summary>
/// <remarks>
/// <para><b>One entry, so a change set is applied in one write.</b> A store
/// that wrote key by key could be interrupted with half the keys new and the
/// ETag saved, and believe itself current. See <see cref="IFreedomStore"/>.</para>
/// <para>An app that wants its plain settings somewhere else — Preferences, a
/// database — implements <see cref="IFreedomStore"/> itself and registers it
/// instead; this is the default, not a requirement.</para>
/// </remarks>
/// <param name="application">The application slug; the entry is scoped to it.</param>
public sealed class SecureStorageFreedomStore(string application) : IFreedomStore
{
	private readonly string _key = "freedom_config_" + application;
	private readonly SemaphoreSlim _lock = new(1, 1);

	/// <inheritdoc/>
	public async Task<FreedomSnapshot> LoadAsync(CancellationToken cancellationToken)
	{
		try
		{
			return FreedomStateJson.DeserializeSnapshot(await SecureStorage.Default.GetAsync(_key).ConfigureAwait(false));
		}
		catch (Exception e) when (e is InvalidOperationException or Java.Lang.Exception)
		{
			// Unreadable is treated as empty: the next sync fetches everything,
			// which is the right answer to a store that has lost its contents.
			return FreedomSnapshot.Empty;
		}
	}

	/// <inheritdoc/>
	public async Task ApplyAsync(FreedomChangeSet changes, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
			await WriteAsync(InMemoryFreedomStore.Apply(current, changes)).ConfigureAwait(false);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <inheritdoc/>
	public async Task MarkVerifiedAsync(string etag, DateTimeOffset at, CancellationToken cancellationToken)
	{
		await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var current = await LoadAsync(cancellationToken).ConfigureAwait(false);
			await WriteAsync(current with { Etag = etag, VerifiedAt = at }).ConfigureAwait(false);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken)
	{
		SecureStorage.Default.Remove(_key);
		return Task.CompletedTask;
	}

	private Task WriteAsync(FreedomSnapshot snapshot) =>
		SecureStorage.Default.SetAsync(_key, FreedomStateJson.Serialize(snapshot));
}
