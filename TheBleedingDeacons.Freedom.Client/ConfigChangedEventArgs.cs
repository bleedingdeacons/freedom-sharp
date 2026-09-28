// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// Raised after a sync changed stored values, so an app can drop anything it cached.
/// </summary>
/// <param name="updated">Keys fetched.</param>
/// <param name="removed">Keys removed.</param>
public sealed class ConfigChangedEventArgs(IReadOnlyList<string> updated, IReadOnlyList<string> removed) : EventArgs
{
	/// <summary>Gets the keys fetched.</summary>
	public IReadOnlyList<string> Updated { get; } = updated;

	/// <summary>Gets the keys removed.</summary>
	public IReadOnlyList<string> Removed { get; } = removed;
}
