// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// Where the app keeps its configuration. The app owns this: Freedom tells it what changed.
/// </summary>
/// <remarks>
/// <para>Every value is stored with its version, and the snapshot with the
/// manifest's ETag and when it was last confirmed with the server. That is how
/// the app answers "how stale is this?" after a start with no signal.</para>
/// <para><see cref="ApplyAsync"/> must be atomic: a partly applied change set
/// with its ETag saved is a device that believes it is current and is not.</para>
/// </remarks>
public interface IFreedomStore
{
	/// <summary>Everything stored, or an empty snapshot.</summary>
	Task<FreedomSnapshot> LoadAsync(CancellationToken cancellationToken);

	/// <summary>Apply a change set in one step.</summary>
	Task ApplyAsync(FreedomChangeSet changes, CancellationToken cancellationToken);

	/// <summary>Record that the server confirmed nothing has changed.</summary>
	Task MarkVerifiedAsync(string etag, DateTimeOffset at, CancellationToken cancellationToken);

	/// <summary>Forget everything.</summary>
	Task ClearAsync(CancellationToken cancellationToken);
}
