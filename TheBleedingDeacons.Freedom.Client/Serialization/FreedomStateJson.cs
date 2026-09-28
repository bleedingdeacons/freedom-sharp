// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Serialization;

/// <summary>
/// How a store persists a snapshot and credentials as text, source-generated
/// so it works in trimmed and AOT builds. Shared by Freedom.Client.Maui's
/// secure-storage stores and any store an app writes itself, so they agree on
/// the shape.
/// </summary>
public static class FreedomStateJson
{
	/// <summary>A snapshot as JSON.</summary>
	public static string Serialize(FreedomSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		return JsonSerializer.Serialize(
			new StoredSnapshot([.. snapshot.Values.Values], snapshot.Etag, snapshot.VerifiedAt),
			FreedomStateJsonContext.Default.StoredSnapshot);
	}

	/// <summary>A snapshot from JSON; empty for null, empty or unreadable text.</summary>
	public static FreedomSnapshot DeserializeSnapshot(string? json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return FreedomSnapshot.Empty;
		}

		try
		{
			var stored = JsonSerializer.Deserialize(json, FreedomStateJsonContext.Default.StoredSnapshot);

			return stored is null
				? FreedomSnapshot.Empty
				: new FreedomSnapshot(stored.Values.ToDictionary(v => v.Key, StringComparer.Ordinal), stored.Etag, stored.VerifiedAt);
		}
		catch (JsonException)
		{
			// Unreadable is treated as empty: the next sync fetches everything,
			// which is the right answer to a store that has lost its contents.
			return FreedomSnapshot.Empty;
		}
	}

	/// <summary>Credentials as JSON.</summary>
	public static string Serialize(TabletCredentials credentials)
	{
		ArgumentNullException.ThrowIfNull(credentials);

		return JsonSerializer.Serialize(credentials, FreedomStateJsonContext.Default.TabletCredentials);
	}

	/// <summary>Credentials from JSON; null for null, empty or unreadable text, which reads as "not signed in".</summary>
	public static TabletCredentials? DeserializeCredentials(string? json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return null;
		}

		try
		{
			return JsonSerializer.Deserialize(json, FreedomStateJsonContext.Default.TabletCredentials);
		}
		catch (JsonException)
		{
			return null;
		}
	}
}
