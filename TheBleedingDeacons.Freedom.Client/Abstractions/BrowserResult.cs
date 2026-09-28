// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// How a browser sign-in ended: a code, the server's refusal, or the user closing the browser.
/// </summary>
/// <param name="Code">The one-time code, when the sign-in succeeded.</param>
/// <param name="Error">The server's <c>error</c> value, e.g. <c>not_authorised</c> or <c>declined</c>.</param>
/// <param name="Cancelled">True when the user closed the browser. Not a failure.</param>
public sealed record BrowserResult(string? Code, string? Error, bool Cancelled)
{
	/// <summary>A code came back.</summary>
	public static BrowserResult Succeeded(string code) => new(code, null, false);

	/// <summary>The server sent the browser back with an error.</summary>
	public static BrowserResult Failed(string error) => new(null, error, false);

	/// <summary>The user closed the browser.</summary>
	public static BrowserResult WasCancelled() => new(null, null, true);
}
