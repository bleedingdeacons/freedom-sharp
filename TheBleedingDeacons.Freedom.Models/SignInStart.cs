// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Models;

/// <summary>
/// The answer to <c>GET /freedom/v1/auth/start</c>: where to send the browser.
/// </summary>
public sealed class SignInStart
{
	/// <summary>Gets the state Fellowship holds for this sign-in.</summary>
	public string State { get; init; } = string.Empty;

	/// <summary>Gets the provider URL to open in the browser.</summary>
	public string AuthorizationUrl { get; init; } = string.Empty;
}
