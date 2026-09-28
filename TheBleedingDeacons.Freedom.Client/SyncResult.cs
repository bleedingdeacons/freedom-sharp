// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// The outcome of <see cref="FreedomClient.SyncAsync"/>.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Updated">Keys fetched this time.</param>
/// <param name="Removed">Keys removed this time.</param>
/// <param name="Unopened">Secrets that arrived and could not be opened.</param>
/// <param name="VerifiedAt">When the server last confirmed the stored values; after an offline start, how old they are.</param>
/// <param name="Message">A sentence for a log or a status line. Never a value.</param>
public sealed record SyncResult(
	SyncStatus Status,
	IReadOnlyList<string> Updated,
	IReadOnlyList<string> Removed,
	IReadOnlyList<string> Unopened,
	DateTimeOffset? VerifiedAt,
	string Message)
{
	/// <summary>A result that changed nothing.</summary>
	public static SyncResult Nothing(SyncStatus status, DateTimeOffset? verifiedAt, string message) =>
		new(status, [], [], [], verifiedAt, message);
}
