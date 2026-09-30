// Copyright (c) The Bleeding Deacons. Licensed under the MIT license.

using Microsoft.Maui.Storage;
using TheBleedingDeacons.Freedom.Client.Abstractions;
using TheBleedingDeacons.Freedom.Client.Serialization;

namespace TheBleedingDeacons.Freedom.Client.Maui;

/// <summary>
/// The token and private key, in platform secure storage — Android Keystore-
/// backed encrypted preferences.
/// </summary>
/// <remarks>
/// <para><b>An uninstall loses them</b>, and that is fine here, unlike for
/// Link: the server re-seals every secret on every fetch to whatever key the
/// device holds now, so signing in again gets everything back.</para>
/// <para>A read that throws — a keystore invalidated by a changed screen lock,
/// say — is treated as "not signed in", so the app asks the user to sign in
/// again rather than failing to start.</para>
/// </remarks>
/// <param name="application">The application slug; keys are scoped to it.</param>
public sealed class SecureStorageCredentialStore(string application) : IFreedomCredentialStore
{
	private readonly string _key = "freedom_credentials_" + application;

	/// <inheritdoc/>
	public async Task<TabletCredentials?> LoadAsync(CancellationToken cancellationToken)
	{
		try
		{
			return FreedomStateJson.DeserializeCredentials(await SecureStorage.Default.GetAsync(_key).ConfigureAwait(false));
		}
		catch (Exception e) when (StoreFailures.IsUnreadable(e))
		{
			return null;
		}
	}

	/// <inheritdoc/>
	public Task SaveAsync(TabletCredentials credentials, CancellationToken cancellationToken) =>
		SecureStorage.Default.SetAsync(_key, FreedomStateJson.Serialize(credentials));

	/// <inheritdoc/>
	public Task ClearAsync(CancellationToken cancellationToken)
	{
		SecureStorage.Default.Remove(_key);
		return Task.CompletedTask;
	}
}
