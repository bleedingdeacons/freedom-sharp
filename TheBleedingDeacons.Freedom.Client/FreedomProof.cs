// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// How a device proves itself at sign-in.
/// </summary>
public abstract record FreedomProof
{
	private FreedomProof()
	{
	}

	/// <summary>A Google sign-in in the browser. What a standalone app uses.</summary>
	public static FreedomProof Browser { get; } = new BrowserProof();

	/// <summary>
	/// A session the app already holds with the same site, in place of a second
	/// sign-in. Link hands over its Fellowship device token this way. Opaque: the
	/// server checks it and this library knows nothing about where it came from.
	/// </summary>
	public static FreedomProof ExistingSession(string token) => new SessionProof(token);

	/// <summary>A browser sign-in.</summary>
	public sealed record BrowserProof : FreedomProof;

	/// <summary>An existing session's bearer token.</summary>
	/// <param name="Token">The token.</param>
	public sealed record SessionProof(string Token) : FreedomProof
	{
		/// <summary>Hides the token from logs.</summary>
		public override string ToString() => "SessionProof { Token = *** }";
	}
}
