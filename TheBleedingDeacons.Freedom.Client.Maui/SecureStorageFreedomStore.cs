// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Text.Json;
using Microsoft.Maui.Storage;
using TheBleedingDeacons.Freedom.Client.Abstractions;
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
			var json = await SecureStorage.Default.GetAsync(_key).ConfigureAwait(false);
			if (string.IsNullOrEmpty(json))
			{
				return FreedomSnapshot.Empty;
			}

			var stored = JsonSerializer.Deserialize<Stored>(json);

			return stored is null
				? FreedomSnapshot.Empty
				: new FreedomSnapshot(stored.Values.ToDictionary(v => v.Key, StringComparer.Ordinal), stored.Etag, stored.VerifiedAt);
		}
		catch (Exception e) when (e is JsonException or InvalidOperationException or Java.Lang.Exception)
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
		SecureStorage.Default.SetAsync(_key, JsonSerializer.Serialize(new Stored([.. snapshot.Values.Values], snapshot.Etag, snapshot.VerifiedAt)));

	private sealed record Stored(List<FreedomValue> Values, string? Etag, DateTimeOffset? VerifiedAt);
}
