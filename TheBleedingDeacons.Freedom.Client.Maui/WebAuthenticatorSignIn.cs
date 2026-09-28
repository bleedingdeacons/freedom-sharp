// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Microsoft.Maui.Authentication;
using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Maui;

/// <summary>
/// The browser sign-in, through MAUI's <see cref="WebAuthenticator"/>: a
/// Chrome custom tab that closes itself when the redirect arrives.
/// </summary>
/// <remarks>
/// <para>The redirect is caught by a <c>WebAuthenticatorCallbackActivity</c>
/// subclass in the app, whose intent filter names the callback URI's scheme
/// and host. The library cannot declare it, because the scheme is the app's.
/// A mismatch shows up as a browser tab that never comes back, with nothing in
/// any log — see the README's "The three places the callback is written".</para>
/// <para>What comes back is a one-time code, useless without the PKCE verifier
/// the client kept, so another app that claims the same scheme gains nothing.</para>
/// </remarks>
public sealed class WebAuthenticatorSignIn : IFreedomSignIn
{
	/// <inheritdoc/>
	public async Task<BrowserResult> AuthenticateAsync(Uri authorizationUrl, Uri callbackUri, CancellationToken cancellationToken)
	{
		try
		{
			var result = await WebAuthenticator.Default.AuthenticateAsync(
				new WebAuthenticatorOptions { Url = authorizationUrl, CallbackUrl = callbackUri, PrefersEphemeralWebBrowserSession = true },
				cancellationToken).ConfigureAwait(false);

			if (result.Properties.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code))
			{
				return BrowserResult.Succeeded(code);
			}

			return BrowserResult.Failed(result.Properties.TryGetValue("error", out var error) && !string.IsNullOrEmpty(error) ? error : "verification");
		}
		catch (TaskCanceledException)
		{
			// The user closed the tab. Not a failure, and nothing to say.
			return BrowserResult.WasCancelled();
		}
	}
}
