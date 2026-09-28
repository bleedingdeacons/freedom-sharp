// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TheBleedingDeacons.Freedom.Client.Abstractions;
using TheBleedingDeacons.Freedom.Client.Crypto;
using TheBleedingDeacons.Freedom.Models;

namespace TheBleedingDeacons.Freedom.Client;

/// <summary>
/// Signs a device in, and keeps its configuration current on every start.
/// </summary>
/// <remarks>
/// <para><b>Every start:</b> call <see cref="SyncAsync"/>. On most starts
/// nothing has changed, and that costs one small request answered with a 304.
/// Otherwise only the keys whose version differs are fetched, and keys the
/// server no longer lists are removed.</para>
/// <para><b>Only a refusal clears anything.</b> No network, a timeout, a 500,
/// a 429, a disabled application: the device keeps what it has and says how
/// old it is (<see cref="SyncResult.VerifiedAt"/>). A revoked token, an account
/// that may no longer use the application or a blocked device clears the
/// store and the credentials — unless <see cref="FreedomOptions.ClearOnRefusal"/>
/// is off.</para>
/// <para><b>The ETag is saved last</b>, and only when every stale key was
/// applied, so a sync interrupted part-way is repaired by the next start
/// rather than mistaken for current.</para>
/// <para>Calls are serialised: two overlapping syncs, or a sync overlapping a
/// sign-in, run one after the other.</para>
/// </remarks>
public sealed class FreedomClient : IDisposable
{
	private const int MaxKeysPerRequest = 100;

	private readonly FreedomOptions _options;
	private readonly IFreedomStore _store;
	private readonly IFreedomCredentialStore _credentials;
	private readonly IFreedomSignIn? _signIn;
	private readonly IDeviceIdentity? _device;
	private readonly FreedomApi? _api;
	private readonly ILogger _logger;
	private readonly TimeProvider _time;
	private readonly SemaphoreSlim _gate = new(1, 1);

	private FreedomSnapshot _current = FreedomSnapshot.Empty;

	/// <summary>
	/// Initializes a new instance of the <see cref="FreedomClient"/> class.
	/// </summary>
	/// <param name="options">The site, the application and the callback.</param>
	/// <param name="store">Where configuration is kept.</param>
	/// <param name="credentials">Where the token and private key are kept.</param>
	/// <param name="signIn">The browser sign-in. Optional for an app that only hands over an existing session.</param>
	/// <param name="device">This device's identity. Required for a browser sign-in.</param>
	/// <param name="httpClient">A client to share.</param>
	/// <param name="logger">A logger. Values and tokens are never logged.</param>
	/// <param name="time">A clock, for tests.</param>
	public FreedomClient(
		FreedomOptions options,
		IFreedomStore store,
		IFreedomCredentialStore credentials,
		IFreedomSignIn? signIn = null,
		IDeviceIdentity? device = null,
		HttpClient? httpClient = null,
		ILogger<FreedomClient>? logger = null,
		TimeProvider? time = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_store = store ?? throw new ArgumentNullException(nameof(store));
		_credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
		_signIn = signIn;
		_device = device;
		_logger = (ILogger?)logger ?? NullLogger.Instance;
		_time = time ?? TimeProvider.System;

		// Unconfigured options are reported by every call rather than thrown
		// here: an app whose settings were left out of a build should start and
		// say so on its setup screen, not crash on launch.
		_api = options.IsConfigured
			? new FreedomApi(options.BaseUrl, httpClient, _logger, options.AllowInsecureBaseUrl, userAgent: options.UserAgent ?? FreedomApi.DefaultUserAgent(options.Application))
			: null;
	}

	/// <summary>Raised after a sync changed stored values.</summary>
	public event EventHandler<ConfigChangedEventArgs>? ConfigChanged;

	/// <summary>Gets what the store held after the last load or sync. Call <see cref="LoadAsync"/> or <see cref="SyncAsync"/> first.</summary>
	public FreedomSnapshot Current => Volatile.Read(ref _current);

	/// <summary>A value from <see cref="Current"/>, or null when it is not held.</summary>
	public string? Get(string key) => Current.Values.TryGetValue(key, out var value) ? value.Value : null;

	/// <summary>A value from <see cref="Current"/>, with its version and whether it was secret.</summary>
	public bool TryGet(string key, out FreedomValue value)
	{
		if (Current.Values.TryGetValue(key, out var found))
		{
			value = found;
			return true;
		}

		value = null!;
		return false;
	}

	/// <summary>Read the store into <see cref="Current"/> without contacting the server.</summary>
	public async Task<FreedomSnapshot> LoadAsync(CancellationToken cancellationToken = default)
	{
		var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
		Volatile.Write(ref _current, snapshot);

		return snapshot;
	}

	/// <summary>Whether this device holds credentials.</summary>
	public async Task<bool> IsEnrolledAsync(CancellationToken cancellationToken = default) =>
		await _credentials.LoadAsync(cancellationToken).ConfigureAwait(false) is not null;

	/// <summary>
	/// Sign this device in, then sync. With no proof, a browser sign-in.
	/// </summary>
	/// <remarks>
	/// A fresh keypair is made for every sign-in, and a sign-in is the only way
	/// to replace one — so a device that reports a key fault is cured by this.
	/// Stored values are kept across a sign-in; the sync that follows fetches
	/// whatever changed.
	/// </remarks>
	public async Task<EnrolmentResult> EnrolAsync(FreedomProof? proof = null, CancellationToken cancellationToken = default)
	{
		proof ??= FreedomProof.Browser;

		if (_api is null)
		{
			return new EnrolmentResult(EnrolmentStatus.NotConfigured, null, "Freedom is not configured: the site address or the application is missing.", null);
		}

		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		EnrolmentResult enrolled;
		try
		{
			enrolled = proof switch
			{
				FreedomProof.SessionProof session => await HandOverAsync(_api, session.Token, cancellationToken).ConfigureAwait(false),
				_ => await SignInThroughBrowserAsync(_api, cancellationToken).ConfigureAwait(false),
			};
		}
		finally
		{
			_gate.Release();
		}

		if (!enrolled.Succeeded)
		{
			return enrolled;
		}

		var sync = await SyncAsync(cancellationToken).ConfigureAwait(false);

		return enrolled with { Sync = sync };
	}

	/// <summary>
	/// Bring stored configuration up to date. Call on every start.
	/// </summary>
	public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			return await SyncLockedAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>
	/// Sign this device out: revoke its token on the server, and clear the
	/// credentials and the stored configuration here. The local clear happens
	/// whether or not the server could be told.
	/// </summary>
	public async Task SignOutAsync(CancellationToken cancellationToken = default)
	{
		await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var credentials = await _credentials.LoadAsync(cancellationToken).ConfigureAwait(false);
			if (credentials is not null && _api is not null)
			{
				await _api.SignOutAsync(credentials.Token, cancellationToken).ConfigureAwait(false);
			}

			await ClearAsync(cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		_api?.Dispose();
		_gate.Dispose();
	}

	private async Task<EnrolmentResult> SignInThroughBrowserAsync(FreedomApi api, CancellationToken cancellationToken)
	{
		if (_signIn is null || _device is null || _options.CallbackUri is null)
		{
			return new EnrolmentResult(EnrolmentStatus.NotConfigured, null, "A browser sign-in needs a callback URI, a sign-in and a device identity.", null);
		}

		var device = await _device.GetAsync(cancellationToken).ConfigureAwait(false);
		var verifier = Pkce.NewVerifier();

		var start = await api.StartSignInAsync(_options.Application, _options.CallbackUri, Pkce.ChallengeFor(verifier), _options.Provider, cancellationToken).ConfigureAwait(false);
		if (!start.Success || start.Data is null)
		{
			return Failure(start);
		}

		BrowserResult browser;
		try
		{
			browser = await _signIn.AuthenticateAsync(new Uri(start.Data.AuthorizationUrl), _options.CallbackUri, cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			browser = BrowserResult.WasCancelled();
		}

		if (browser.Cancelled)
		{
			return new EnrolmentResult(EnrolmentStatus.Cancelled, null, "Sign-in was cancelled.", null);
		}

		if (browser.Code is null)
		{
			var error = browser.Error ?? "verification";
			return new EnrolmentResult(EnrolmentStatus.Refused, error, BrowserRefusal(error), null);
		}

		var keys = TabletKeyPair.Generate();
		var exchanged = await api.ExchangeAsync(_options.Application, browser.Code, verifier, device, keys.PublicKey, cancellationToken).ConfigureAwait(false);

		return await CompleteAsync(exchanged, keys, cancellationToken).ConfigureAwait(false);
	}

	private async Task<EnrolmentResult> HandOverAsync(FreedomApi api, string sessionToken, CancellationToken cancellationToken)
	{
		var device = _device is null ? null : await _device.GetAsync(cancellationToken).ConfigureAwait(false);
		var keys = TabletKeyPair.Generate();

		var handedOver = await api.HandOverSessionAsync(sessionToken, _options.Application, keys.PublicKey, device, cancellationToken).ConfigureAwait(false);

		return await CompleteAsync(handedOver, keys, cancellationToken).ConfigureAwait(false);
	}

	private async Task<EnrolmentResult> CompleteAsync(ApiResponse<Enrolment> response, TabletKeyPair keys, CancellationToken cancellationToken)
	{
		if (!response.Success || response.Data is null || string.IsNullOrEmpty(response.Data.Token))
		{
			return Failure(response);
		}

		await _credentials.SaveAsync(new TabletCredentials(response.Data.Token, response.Data.Tablet.Id, keys.PrivateKeyPem), cancellationToken).ConfigureAwait(false);

		var reattached = response.Data.Tablet.Reattached;
		_logger.LogInformation("Freedom: signed in as tablet {Tablet} ({How})", response.Data.Tablet.Id, reattached ? "re-attached" : "new");

		return new EnrolmentResult(
			reattached ? EnrolmentStatus.Reattached : EnrolmentStatus.Enrolled,
			null,
			reattached ? "Signed in again; this device's settings were kept." : "Signed in.",
			null);
	}

	private async Task<SyncResult> SyncLockedAsync(CancellationToken cancellationToken)
	{
		var snapshot = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
		Volatile.Write(ref _current, snapshot);

		if (_api is null)
		{
			return SyncResult.Nothing(SyncStatus.NotConfigured, snapshot.VerifiedAt, "Freedom is not configured.");
		}

		var credentials = await _credentials.LoadAsync(cancellationToken).ConfigureAwait(false);
		if (credentials is null)
		{
			return SyncResult.Nothing(SyncStatus.NotEnrolled, snapshot.VerifiedAt, "This device has not signed in.");
		}

		var manifest = await _api.GetManifestAsync(credentials.Token, snapshot.Etag, cancellationToken).ConfigureAwait(false);
		var now = _time.GetUtcNow();

		if (manifest.NotModified)
		{
			await _store.MarkVerifiedAsync(snapshot.Etag ?? string.Empty, now, cancellationToken).ConfigureAwait(false);
			await ReloadAsync(cancellationToken).ConfigureAwait(false);

			return SyncResult.Nothing(SyncStatus.UpToDate, now, "Configuration is current.");
		}

		if (!manifest.Success || manifest.Data is null)
		{
			return await RefusedOrKeptAsync(manifest, snapshot, cancellationToken).ConfigureAwait(false);
		}

		return await ApplyManifestAsync(manifest.Data, snapshot, credentials, now, cancellationToken).ConfigureAwait(false);
	}

	private async Task<SyncResult> ApplyManifestAsync(Manifest manifest, FreedomSnapshot snapshot, TabletCredentials credentials, DateTimeOffset now, CancellationToken cancellationToken)
	{
		var listed = new HashSet<string>(manifest.Keys.Select(e => e.Key), StringComparer.Ordinal);

		// Stale: not held, or held at a different version, or its secrecy
		// changed. Versions never repeat within an application, so difference
		// is the test — not order.
		var stale = manifest.Keys
			.Where(e => !snapshot.Values.TryGetValue(e.Key, out var held) || held.Version != e.Version || held.IsSecret != e.Secret)
			.ToList();

		var removed = snapshot.Values.Keys.Where(k => !listed.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();

		var upserts = new List<FreedomValue>();
		var unopened = new List<string>();
		var complete = true;

		foreach (var batch in stale.Chunk(MaxKeysPerRequest))
		{
			var expected = batch.ToDictionary(e => e.Key, StringComparer.Ordinal);
			var response = await _api!.GetValuesAsync(credentials.Token, [.. expected.Keys], cancellationToken).ConfigureAwait(false);

			if (!response.Success || response.Data is null)
			{
				// Keep what the other batches brought; the ETag stays unsaved,
				// so the next start asks again.
				complete = false;
				continue;
			}

			if (response.Data.Missing.Count > 0 || response.Data.Unreadable.Count > 0)
			{
				complete = false;
			}

			foreach (var entry in response.Data.Values)
			{
				if (!expected.TryGetValue(entry.Key, out var listedEntry) || listedEntry.Version != entry.Version)
				{
					// Changed again between the manifest and this answer. The
					// next start picks it up at its new version.
					complete = false;
					continue;
				}

				var opened = Open(entry, credentials);
				if (opened is null)
				{
					unopened.Add(entry.Key);
					complete = false;
					continue;
				}

				upserts.Add(new FreedomValue(entry.Key, opened, entry.Version, entry.Secret));
			}
		}

		if (upserts.Count > 0 || removed.Count > 0 || complete)
		{
			await _store.ApplyAsync(new FreedomChangeSet(upserts, removed, complete ? manifest.Etag : null, now), cancellationToken).ConfigureAwait(false);
		}

		await ReloadAsync(cancellationToken).ConfigureAwait(false);

		var updatedKeys = upserts.Select(v => v.Key).ToList();
		if (updatedKeys.Count > 0 || removed.Count > 0)
		{
			ConfigChanged?.Invoke(this, new ConfigChangedEventArgs(updatedKeys, removed));
		}

		if (unopened.Count > 0)
		{
			// The old value is kept — it was working a moment ago — and the
			// server is told, so the admin list shows this device in red.
			await _api!.ReportKeyFaultAsync(credentials.Token, cancellationToken).ConfigureAwait(false);
			_logger.LogWarning("Freedom: {Count} secret(s) could not be opened; sign in again to replace this device's key", unopened.Count);

			return new SyncResult(SyncStatus.KeyFault, updatedKeys, removed, unopened, now, "Some secrets could not be opened. Sign in again.");
		}

		var status = updatedKeys.Count > 0 || removed.Count > 0 ? SyncStatus.Updated : SyncStatus.UpToDate;
		_logger.LogInformation("Freedom: sync {Status}: {Updated} fetched, {Removed} removed", status, updatedKeys.Count, removed.Count);

		return new SyncResult(status, updatedKeys, removed, [], now, complete ? "Configuration is current." : "Configuration was partly updated; the rest follows at the next start.");
	}

	private async Task<SyncResult> RefusedOrKeptAsync(ApiResponse<Manifest> manifest, FreedomSnapshot snapshot, CancellationToken cancellationToken)
	{
		var code = manifest.Error?.Code ?? string.Empty;

		if (manifest.Unreachable)
		{
			return SyncResult.Nothing(SyncStatus.Offline, snapshot.VerifiedAt, "The server could not be reached; keeping the configuration held.");
		}

		if (manifest.StatusCode == HttpStatusCode.Unauthorized)
		{
			await ClearOnRefusalAsync(cancellationToken).ConfigureAwait(false);
			return SyncResult.Nothing(SyncStatus.Revoked, snapshot.VerifiedAt, "This device is no longer signed in.");
		}

		if (manifest.StatusCode == HttpStatusCode.Forbidden && string.Equals(code, "freedom_application_disabled", StringComparison.Ordinal))
		{
			return SyncResult.Nothing(SyncStatus.Suspended, snapshot.VerifiedAt, manifest.Error?.Message ?? "The application is not currently available.");
		}

		if (manifest.StatusCode == HttpStatusCode.Forbidden && code is "freedom_not_authorised" or "freedom_tablet_blocked")
		{
			await ClearOnRefusalAsync(cancellationToken).ConfigureAwait(false);
			return SyncResult.Nothing(SyncStatus.NotAuthorised, snapshot.VerifiedAt, manifest.Error?.Message ?? "This device may no longer use the application.");
		}

		// Anything else — a 5xx, a 429, a 403 from a firewall, an answer that
		// was not JSON — is not the server refusing this device, and must not
		// cost it its configuration.
		return SyncResult.Nothing(SyncStatus.ServerError, snapshot.VerifiedAt, "The server answered with an error; keeping the configuration held.");
	}

	private async Task ClearOnRefusalAsync(CancellationToken cancellationToken)
	{
		if (!_options.ClearOnRefusal)
		{
			return;
		}

		await ClearAsync(cancellationToken).ConfigureAwait(false);
	}

	private async Task ClearAsync(CancellationToken cancellationToken)
	{
		var held = Current.Values.Keys.ToList();

		await _credentials.ClearAsync(cancellationToken).ConfigureAwait(false);
		await _store.ClearAsync(cancellationToken).ConfigureAwait(false);
		Volatile.Write(ref _current, FreedomSnapshot.Empty);

		if (held.Count > 0)
		{
			ConfigChanged?.Invoke(this, new ConfigChangedEventArgs([], held));
		}
	}

	private async Task ReloadAsync(CancellationToken cancellationToken)
	{
		Volatile.Write(ref _current, await _store.LoadAsync(cancellationToken).ConfigureAwait(false));
	}

	private static string? Open(ValueEntry entry, TabletCredentials credentials)
	{
		if (!entry.Secret)
		{
			return entry.Value;
		}

		return entry.K is null || entry.P is null
			? null
			: EnvelopeOpener.Open(entry.Key, entry.Version, entry.K, entry.P, credentials.PrivateKeyPem);
	}

	private static EnrolmentResult Failure<T>(ApiResponse<T> response)
		where T : class
	{
		if (response.Unreachable)
		{
			return new EnrolmentResult(EnrolmentStatus.Failed, null, "The server could not be reached.", null);
		}

		var status = (int?)response.StatusCode ?? 0;
		var refused = status is >= 400 and < 500 && status != 429;

		return new EnrolmentResult(
			refused ? EnrolmentStatus.Refused : EnrolmentStatus.Failed,
			response.Error?.Code,
			response.Error?.Message ?? "Sign-in failed.",
			null);
	}

	private static string BrowserRefusal(string error) => error switch
	{
		"not_authorised" => "This Google account may not use this app. Sign in with a member's account or one of the app's tablet accounts.",
		"declined" => "Sign-in was declined at the provider.",
		"provider" => "That sign-in provider is not available.",
		_ => "The sign-in could not be verified. Please try again.",
	};
}
