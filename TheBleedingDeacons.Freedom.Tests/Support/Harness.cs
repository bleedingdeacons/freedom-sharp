// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using TheBleedingDeacons.Freedom.Client;
using TheBleedingDeacons.Freedom.Client.Stores;

namespace TheBleedingDeacons.Freedom.Tests.Support;

/// <summary>
/// A device: a client over the fake server, with in-memory stores and a fake
/// browser. Linked into the Specs project, whose World wraps one.
/// </summary>
public sealed class Harness : IDisposable
{
	public Harness(bool clearOnRefusal = true)
	{
		Browser = new FakeBrowser(Server);
		Options = new FreedomOptions
		{
			BaseUrl = new Uri(FakeFreedomServer.BaseUrl),
			Application = "register",
			CallbackUri = new Uri("org.example.register.freedom://auth"),
			ClearOnRefusal = clearOnRefusal,
		};
		Client = Build(Options);
	}

	public FakeFreedomServer Server { get; } = new();

	public FakeBrowser Browser { get; }

	public InMemoryFreedomStore Store { get; } = new();

	public InMemoryCredentialStore Credentials { get; } = new();

	public FreedomOptions Options { get; }

	public FreedomClient Client { get; private set; }

	public List<ConfigChangedEventArgs> Changes { get; } = [];

	/// <summary>A client with other options over the same server and stores.</summary>
	public FreedomClient Build(FreedomOptions options)
	{
		var client = new FreedomClient(options, Store, Credentials, Browser, new FixedDevice(), new HttpClient(Server, disposeHandler: false));
		client.ConfigChanged += (_, e) => Changes.Add(e);
		Client = client;

		return client;
	}

	public async Task<EnrolmentResult> SignInAsync() => await Client.EnrolAsync(cancellationToken: TestContext.Current.CancellationToken);

	public async Task<SyncResult> StartAsync() => await Client.SyncAsync(TestContext.Current.CancellationToken);

	public void Dispose()
	{
		Client.Dispose();
		Server.Dispose();
	}
}
