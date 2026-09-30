// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Hosting;
using TheBleedingDeacons.Freedom.Client.Abstractions;

namespace TheBleedingDeacons.Freedom.Client.Maui;

/// <summary>
/// Everything a MAUI app needs to use Freedom, in one call.
/// </summary>
public static class FreedomMauiExtensions
{
	/// <summary>
	/// Register <see cref="FreedomClient"/> and a working default for every seam
	/// it leaves open. Anything already registered — a store of the app's own,
	/// say — is kept.
	/// </summary>
	/// <example>
	/// <code>
	/// builder.UseFreedom(new FreedomOptions
	/// {
	///     BaseUrl = new Uri("https://aa-bristol.org"),
	///     Application = "register",
	///     CallbackUri = new Uri("org.example.register.freedom://auth"),
	/// });
	/// </code>
	/// Then, on every start: <c>var result = await freedom.SyncAsync();</c> and,
	/// when that says <see cref="SyncStatus.NotEnrolled"/>, <c>await freedom.EnrolAsync();</c>.
	/// </example>
	public static MauiAppBuilder UseFreedom(this MauiAppBuilder builder, FreedomOptions options)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(options);

		var services = builder.Services;

		// The app's own name and version first, then the library's: the
		// traffic is the app's, and "which build is this?" is the first
		// question asked of a misbehaving tablet.
		options = options.UserAgent is null
			? options with { UserAgent = $"{AppInfo.Current.Name.Replace(' ', '-')}/{AppInfo.Current.VersionString} ({FreedomApi.DefaultUserAgent(options.Application)})" }
			: options;

		services.TryAddSingleton(options);
		services.TryAddSingleton<IFreedomStore>(_ => new SecureStorageFreedomStore(options.Application));
		services.TryAddSingleton<IFreedomCredentialStore>(_ => new SecureStorageCredentialStore(options.Application));
		services.TryAddSingleton<IFreedomSignIn, WebAuthenticatorSignIn>();
#if ANDROID
		services.TryAddSingleton<IDeviceIdentity, AndroidDeviceIdentity>();
#endif

		// No device identity off Android unless the app registered one: a
		// session handover needs none, and a browser sign-in says it needs one.
		services.TryAddSingleton(sp => new FreedomClient(
			sp.GetRequiredService<FreedomOptions>(),
			sp.GetRequiredService<IFreedomStore>(),
			sp.GetRequiredService<IFreedomCredentialStore>(),
			sp.GetRequiredService<IFreedomSignIn>(),
			sp.GetService<IDeviceIdentity>(),
			NativeHttpClient(),
			sp.GetService<ILogger<FreedomClient>>()));

		return builder;
	}

	/// <summary>
	/// On Android, Android's own HTTP stack rather than .NET's managed one, as
	/// Link uses: the edge firewall in front of the suite's sites fingerprints
	/// the TLS handshake, and the platform stack is the one the system browser
	/// uses. Elsewhere the managed stack, whose first-handshake refusal
	/// FreedomApi already retries past. An app that wants its own platform
	/// stack constructs FreedomClient itself. One client for the process —
	/// Freedom asks once per start.
	/// </summary>
	private static HttpClient NativeHttpClient() =>
#if ANDROID
		new(new Xamarin.Android.Net.AndroidMessageHandler(), disposeHandler: true) { Timeout = TimeSpan.FromSeconds(20) };
#else
		new() { Timeout = TimeSpan.FromSeconds(20) };
#endif
}
