// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// The only things an app using Freedom has built in. None of them is a secret.
/// </summary>
/// <remarks>
/// <para>That is the point of the library. An app that used to ship an SMTP
/// password or an API key in its package ships these three instead, and asks
/// for the rest.</para>
/// <para><see cref="CallbackUri"/> must be written identically in three places:
/// here, in the Android intent filter that catches it, and in the application's
/// details in the Freedom admin. Google's console is not one of them — the
/// provider returns to Fellowship, which returns to this.</para>
/// </remarks>
public sealed record FreedomOptions
{
	/// <summary>Gets the WordPress site's address, e.g. <c>https://aa-bristol.org</c>. HTTPS unless <see cref="AllowInsecureBaseUrl"/>.</summary>
	public required Uri BaseUrl { get; init; }

	/// <summary>Gets the application's slug, e.g. <c>register</c>.</summary>
	public required string Application { get; init; }

	/// <summary>Gets where a browser sign-in returns to the app, e.g. <c>org.example.register.freedom://auth</c>. Not used by a session hand-off.</summary>
	public Uri? CallbackUri { get; init; }

	/// <summary>Gets the sign-in provider. Fellowship registers <c>google</c>, <c>microsoft</c> and <c>facebook</c> for a browser sign-in.</summary>
	public string Provider { get; init; } = "google";

	/// <summary>Gets a value indicating whether a plain-http <see cref="BaseUrl"/> is allowed. For a local development site only.</summary>
	public bool AllowInsecureBaseUrl { get; init; }

	/// <summary>
	/// Gets a value indicating whether a refusal clears the stored configuration and credentials.
	/// On by default: a revoked or refused device should not go on using secrets it was sent.
	/// </summary>
	public bool ClearOnRefusal { get; init; } = true;

	/// <summary>Gets a value indicating whether these options are complete enough to talk to a server.</summary>
	public bool IsConfigured => IsAllowedBaseUrl(BaseUrl, AllowInsecureBaseUrl) && !string.IsNullOrWhiteSpace(Application);

	/// <summary>Whether a base URL is one Freedom will talk to: absolute, and https unless plain http is allowed.</summary>
	internal static bool IsAllowedBaseUrl(Uri? baseUrl, bool allowInsecure) =>
		baseUrl is not null
		&& baseUrl.IsAbsoluteUri
		&& (string.Equals(baseUrl.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
			|| (allowInsecure && string.Equals(baseUrl.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)));
}
