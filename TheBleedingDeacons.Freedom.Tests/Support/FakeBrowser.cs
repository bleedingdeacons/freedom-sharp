// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Tests.Support;

/// <summary>
/// A browser that goes straight to the callback with whatever the fake server
/// would have sent — or is closed by its user.
/// </summary>
public sealed class FakeBrowser(FakeFreedomServer server) : IFreedomSignIn
{
	public bool Closes { get; set; }

	public int Opened { get; private set; }

	public Task<BrowserResult> AuthenticateAsync(Uri authorizationUrl, Uri callbackUri, CancellationToken cancellationToken)
	{
		Opened++;

		if (Closes)
		{
			throw new TaskCanceledException("The user closed the browser.");
		}

		var outcome = server.BrowserOutcome();
		return Task.FromResult(outcome.StartsWith("code=", StringComparison.Ordinal)
			? BrowserResult.Succeeded(outcome["code=".Length..])
			: BrowserResult.Failed(outcome["error=".Length..]));
	}
}
