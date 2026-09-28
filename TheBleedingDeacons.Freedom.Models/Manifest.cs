// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// The answer to <c>GET /freedom/v1/config/manifest</c>: every key the tablet
/// should hold, each with its version. Never a value.
/// </summary>
public sealed class Manifest
{
	/// <summary>Gets the application's slug.</summary>
	public string Application { get; init; } = string.Empty;

	/// <summary>Gets the tablet's id.</summary>
	public long Tablet { get; init; }

	/// <summary>Gets the manifest's ETag, to send back as If-None-Match.</summary>
	public string Etag { get; init; } = string.Empty;

	/// <summary>Gets when the server built this answer, as Unix seconds.</summary>
	public long CheckedAt { get; init; }

	/// <summary>Gets every key the tablet should hold. Complete: a key not listed has been removed.</summary>
	public IReadOnlyList<ManifestEntry> Keys { get; init; } = [];
}
