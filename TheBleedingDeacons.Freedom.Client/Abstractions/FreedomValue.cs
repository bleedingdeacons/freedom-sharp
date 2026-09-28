// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// One stored configuration value.
/// </summary>
/// <param name="Key">The key, e.g. <c>smtp.host</c>.</param>
/// <param name="Value">The value — for a secret, already opened.</param>
/// <param name="Version">The version it was served at.</param>
/// <param name="IsSecret">Whether it arrived sealed. An app storing values may want to treat these differently.</param>
public sealed record FreedomValue(string Key, string Value, long Version, bool IsSecret)
{
	/// <summary>Hides a secret value from logs and debugger displays.</summary>
	public override string ToString() => $"FreedomValue {{ Key = {Key}, Version = {Version}, Value = {(IsSecret ? "***" : Value)} }}";
}
