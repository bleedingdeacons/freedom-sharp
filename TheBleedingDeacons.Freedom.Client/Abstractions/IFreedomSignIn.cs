// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client.Abstractions;

/// <summary>
/// Opens the provider's sign-in page and waits for the browser to come back to the app.
/// </summary>
/// <remarks>
/// On a MAUI device that is <c>WebAuthenticator</c> — see Freedom.Client.Maui's
/// <c>WebAuthenticatorSignIn</c>. On a desktop or in a console it is a loopback
/// listener; the example CLI carries one.
/// </remarks>
public interface IFreedomSignIn
{
	/// <summary>Open <paramref name="authorizationUrl"/> and wait for a redirect to <paramref name="callbackUri"/>.</summary>
	Task<BrowserResult> AuthenticateAsync(Uri authorizationUrl, Uri callbackUri, CancellationToken cancellationToken);
}
