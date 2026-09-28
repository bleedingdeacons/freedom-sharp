// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
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

		services.TryAddSingleton(options);
		services.TryAddSingleton<IFreedomStore>(_ => new SecureStorageFreedomStore(options.Application));
		services.TryAddSingleton<IFreedomCredentialStore>(_ => new SecureStorageCredentialStore(options.Application));
		services.TryAddSingleton<IFreedomSignIn, WebAuthenticatorSignIn>();
		services.TryAddSingleton<IDeviceIdentity, AndroidDeviceIdentity>();
		services.TryAddSingleton(sp => new FreedomClient(
			sp.GetRequiredService<FreedomOptions>(),
			sp.GetRequiredService<IFreedomStore>(),
			sp.GetRequiredService<IFreedomCredentialStore>(),
			sp.GetRequiredService<IFreedomSignIn>(),
			sp.GetRequiredService<IDeviceIdentity>(),
			logger: sp.GetService<ILogger<FreedomClient>>()));

		return builder;
	}
}
