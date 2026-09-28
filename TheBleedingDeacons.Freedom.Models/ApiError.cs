// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// A WordPress REST error body.
/// </summary>
public sealed class ApiError
{
	/// <summary>Gets the machine-readable code, e.g. <c>freedom_not_authorised</c>.</summary>
	public required string Code { get; init; }

	/// <summary>Gets the human-readable message, in the server's words.</summary>
	public required string Message { get; init; }
}
