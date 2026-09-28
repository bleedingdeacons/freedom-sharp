# freedom-sharp

[![CI](https://github.com/bleedingdeacons/freedom-sharp/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/bleedingdeacons/freedom-sharp/actions/workflows/ci.yml)
[![Semgrep](https://github.com/bleedingdeacons/freedom-sharp/actions/workflows/semgrep.yml/badge.svg?branch=main)](https://github.com/bleedingdeacons/freedom-sharp/actions/workflows/semgrep.yml)
[![Coverage Status](https://coveralls.io/repos/github/bleedingdeacons/freedom-sharp/badge.svg?branch=main)](https://coveralls.io/github/bleedingdeacons/freedom-sharp?branch=main)

Zero-configuration settings for .NET apps. The app has three things built
in, none of them secret: a WordPress site's address, an application slug,
and a callback URI. Everything else it fetches from the site's
[Freedom](https://github.com/bleedingdeacons/freedom) plugin.

- On first run, the device signs in with a Google account.
- On every start, it asks which settings changed and fetches only those.
- Secrets arrive sealed to a key only that device holds.

This README is the specification, argued rather than listed. The
[domain model](specs/domain-model.md) has the vocabulary, the locked
decisions and what is still open. The
[feature files](TheBleedingDeacons.Freedom.Specs/Features) are the
executable half; where this and they disagree, they are what runs.

## What it does

Register, the intergroup's attendance tablet app, used to ship with its
SMTP password, Unity API key and Better Stack token inside the APK:

- anybody with a copy of the APK had the credentials;
- changing any of them took a new build and a reinstall on every tablet.

With Freedom the tablet signs in once, and on every start:

1. **Asks for the manifest:** every key it should hold, each with a
   version, and no values.
2. **Fetches only the keys whose version differs** from what it holds.
3. **Removes any key it holds that the manifest no longer lists.**
4. **Opens the secrets** with its own private key.
5. **Hands the result to the app's store** in one step, and raises
   `ConfigChanged`.

On most starts nothing has changed. That costs one request, answered with
a `304` and no body.

## What it does not do

**It is not a vault.** The app stores what it receives, and a device that
has been sent a secret knows it:

- Revoking the device stops future values. It does not take back past
  ones.
- The server can read every value. That is what lets an admin set them.
- For a credential that must be taken back, rotate the credential itself;
  Freedom makes that a one-field edit on the site.

## The only things built in

```csharp
new FreedomOptions
{
    BaseUrl     = new Uri("https://aa-bristol.org"),                 // https, always
    Application = "register",                                         // the slug
    CallbackUri = new Uri("org.example.register.freedom://auth"),     // a custom scheme
}
```

- **https only.** `AllowInsecureBaseUrl` exists for a local development
  site, and nothing else.
- **The REST namespace is appended, never configured.** A site in a
  subdirectory keeps its path: `https://host/amber` becomes
  `https://host/amber/wp-json/freedom/v1/…`.
- **Options that cannot work are reported, not thrown.** An app whose
  settings were left out of a build starts and says so
  (`SyncStatus.NotConfigured`); it does not crash on launch.

## Using it on its own

freedom-sharp does not depend on Link, Fellowship's client, or any other
package of this suite. A MAUI app needs:

```csharp
// MauiProgram.cs
builder.UseFreedom(new FreedomOptions { BaseUrl = …, Application = "register", CallbackUri = … });

// On every start
var freedom = services.GetRequiredService<FreedomClient>();
var result  = await freedom.SyncAsync();
if (result.Status == SyncStatus.NotEnrolled)
{
    await freedom.EnrolAsync();          // opens the browser; see below
}

var host = freedom.Get("smtp.host");
```

It also needs **one Android activity** to catch the browser's return. The
library cannot declare it, because the scheme is the app's:

```csharp
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = "org.example.register.freedom", DataHost = "auth")]
public sealed class FreedomCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity { }
```

`UseFreedom` registers a working default for every seam, and keeps
anything the app registered first:

| Seam | Default (Freedom.Client.Maui) |
| --- | --- |
| `IFreedomSignIn` | `WebAuthenticatorSignIn`: a Chrome custom tab that closes itself |
| `IDeviceIdentity` | `AndroidDeviceIdentity`: `ANDROID_ID`, model, app version |
| `IFreedomCredentialStore` | `SecureStorageCredentialStore` |
| `IFreedomStore` | `SecureStorageFreedomStore`: the whole configuration as one secure-storage entry |

Freedom.Client itself is plain `net10.0` and runs anywhere.
[`example/Freedom-cli`](example/Freedom-cli) is a console app that signs in
through a loopback listener and keeps its state in a file. It references
nothing of this suite's but the client; CI builds it to prove that.

## The three places the callback is written

Written identically in all three, or the browser tab opens and never
comes back, with nothing in any log to say why:

1. `FreedomOptions.CallbackUri`
2. the Android activity's intent filter (`DataScheme`, `DataHost`)
3. the application's **Callback URI** in the Freedom admin

**Google's console is not one of them.** The provider returns to
Fellowship's own HTTPS callback, which is already registered, and
Fellowship sends the browser on to the app.

## Signing a device in

1. `EnrolAsync()` makes a fresh RSA-2048 keypair and a PKCE verifier.
2. It asks the site to start a sign-in, sending only the verifier's
   SHA-256.
3. It opens the browser. The user signs in with Google.
4. Fellowship's callback asks Freedom whether this account may use the
   application: a Unity member's own account, or one of the application's
   common tablet accounts. Both are always accepted, with no approval step.
5. The browser comes back to the app with a **one-time code**.
6. The app sends the code, the verifier, the public key and the device's
   identity, and gets back a **token**.
7. The token and the private key go to the credential store, and the first
   sync runs.

**The code is worthless without the verifier.** A custom URI scheme can be
claimed by any app on the device. An app that catches the code has nothing
to spend it with.

**A cancel is not a failure.** Closing the browser is
`EnrolmentStatus.Cancelled`, and there is nothing to tell anybody. A
refusal carries the server's own words (`EnrolmentResult.Message`), fit to
show.

**Signing in again re-attaches.**

- A device identifies itself by `ANDROID_ID`, which survives a reinstall.
- Signing in again finds the device's old row on the server, gets a new
  token and a new key, and keeps the overrides an admin gave it.
- Signing in again is also the only way to replace the key, so it is the
  cure for a key fault.

### Handing over a session instead

Link already signs in through Google, for Fellowship. Making a member sign
in twice would prove nothing new, so an app that holds a session with the
same site hands it over:

```csharp
await freedom.EnrolAsync(FreedomProof.ExistingSession(fellowshipDeviceToken));
```

- There is no browser. `IFreedomSignIn` is not needed at all.
- The token is opaque here. The server checks it, and this library knows
  nothing about where it came from.
- The application must have **Accept a Link session** ticked.
- The device keeps working only while the Link enrolment is live. When
  Link signs out or is revoked, the next start is `Revoked`, and the app
  hands over its new session once Link has signed in again.

## Every start

`SyncAsync()`, serialised against any other call on the same client:

1. **No credentials:** `NotEnrolled`.
2. **Ask for the manifest**, sending the stored ETag as `If-None-Match`.
   - `304`: record the time and return `UpToDate`.
   - Anything that is not a manifest: see "Only a refusal clears
     anything" below.
3. **Stale keys** are those not held, held at a different version, or
   whose secret flag changed. **Removed keys** are those held but not
   listed.
4. **Fetch the stale keys**, up to 100 per request.
   - Plain values come as they are.
   - Secrets are opened with the private key. A secret whose inner key or
     version is not the one it arrived under is refused.
5. **Apply** the upserts and removals to the store in one step. Save the
   manifest's ETag **only if every stale key applied**. Raise
   `ConfigChanged`.
6. **A secret that will not open:**
   - its old value is kept, because it was working a moment ago;
   - the server is told, so the device shows in red on the admin list;
   - the start reports `KeyFault`. Sign in again.

### Versions, and why they never repeat

Every write to any value in an application takes the application's next
**revision**, and the value is stamped with it. So a version is never
reused within an application, and **the device compares versions for
difference, not order.**

That is what makes overrides work with no bookkeeping on the device:

- **Removing an override** hands the device the default at the default's
  own version. That is lower than the override's, and still different, so
  it is fetched.
- **Changing a default under an override** changes nothing for that
  device, because the override still wins.

### The ETag is saved last

A sync interrupted part-way (a batch that failed, a secret that would not
open, a value that changed again mid-sync) leaves the ETag unsaved. The
next start therefore sees a manifest that differs and repairs it. A store
whose contents still exactly match an older ETag keeps that one, which is
harmless.

## Offline is normal

A tablet in a church hall with no signal starts with what it has.
`SyncResult.VerifiedAt` says how old that is.

| The manifest request… | Status | Stored configuration |
| --- | --- | --- |
| is answered `304` | `UpToDate` | kept, marked verified |
| gets no answer at all | `Offline` | **kept** |
| gets a 5xx, a 429, or an answer that is not JSON | `ServerError` | **kept** |
| gets a 403 without one of the plugin's codes (a firewall) | `ServerError` | **kept** |
| gets `403 freedom_application_disabled` | `Suspended` | **kept** |
| gets `401` | `Revoked` | cleared |
| gets `403 freedom_not_authorised` / `freedom_tablet_blocked` | `NotAuthorised` | cleared |

Reads retry twice on a network error, a 5xx or a 429, half a second apart.
That is deliberately few: a start that waits half a minute on a server
that is down is worse than one that carries on.

## Only a refusal clears anything

**A refusal clears the store and the credentials**, and `ConfigChanged`
reports everything as removed. A refusal is a revoked token, an account
that may no longer use the application, or a blocked device. A device
taken out of service should not go on holding an SMTP password.

`FreedomOptions.ClearOnRefusal = false` turns that off, for an app that
would rather keep working on stale settings. That is a decision to take
deliberately.

## Secrets, stated precisely

| Where | How |
| --- | --- |
| On the site | Encrypted at rest (AES-256-GCM, key derived from the site's auth salt). The site can read them. |
| On the wire | HTTPS only. |
| A secret value | Sealed per value, per fetch, to this device's RSA-2048 public key, in **Fellowship's envelope**: a fresh AES-256-GCM content key, gzip underneath, the key wrapped with RSA-OAEP. `k` is the wrapped key; `p` is nonce(12) \| tag(16) \| ciphertext; both base64. The key and version are inside the seal. |
| On the device | In the `IFreedomStore`: on MAUI, `SecureStorage` (Android Keystore-backed encrypted preferences). |

**OAEP is SHA-1, deliberately.** PHP's `OPENSSL_PKCS1_OAEP_PADDING`
offers nothing else. OAEP relies on preimage resistance, which SHA-1
still has. Changing `EnvelopeOpener` to `OaepSHA256` gives values that
arrive and silently will not open, and `EnvelopeOpenerTests.OaepMustBeSha1`
fails if anybody tries.

**Each side's test does the other side's job.**
`TheBleedingDeacons.Freedom.Tests/Support/Sealing.cs` seals the way the
plugin does, and the plugin's `ConfigApiTest` opens the way
`EnvelopeOpener` does. The three JSON fixtures under `Fixtures/` are
committed identically in the plugin's `tests/fixtures`, and each side's
tests read them.

**The private key is not in the hardware keystore.** It is generated in
managed code and kept in secure storage: the same compromise Link makes.
A rooted device can read it.

## Tablet identity, and why `ANDROID_ID`

- **It survives a reinstall**, so a reinstalled tablet re-attaches to its
  row and keeps its overrides.
- **It is scoped to the app's signing key** on Android 8 and later, and
  the server stores only a keyed hash of it.
- **A debug build and a release build are different tablets**, because
  they are signed differently.
- **A factory reset makes a new tablet.**
- **It is the app's word, not proof.** The Google account is the proof.
  Anybody holding an authorised account could claim another tablet's
  identifier and take over its row. The admin list shows re-attachments,
  and Block is the answer if one was not expected.

## Wire

Everything is under `{BaseUrl}/wp-json/freedom/v1/`.

| Route | Sent | Answer |
| --- | --- | --- |
| `GET auth/start?application=&redirect_uri=&code_challenge=&provider=` | — | `{state, authorization_url}` |
| `POST auth/exchange` | `{application, code, code_verifier, device_id, public_key, label, platform, model, app_version}` | `201`/`200` `{token, tablet:{id, label, created_at, reattached}, application:{slug, name}}` |
| `POST auth/session` | Bearer the existing session; `{application, public_key, label?, platform?, model?, app_version?}` | the same |
| `GET config/manifest` | Bearer; `If-None-Match` | `{application, tablet, etag, checked_at, keys:[{key, version, secret}]}`, or `304` |
| `POST config/values` | Bearer; `{keys:[…≤100]}` | `{values:[{key, version, secret:false, value} \| {key, version, secret:true, k, p}], missing, unreadable}` |
| `POST tablet/key-fault` | Bearer | `{recorded:true}` |
| `DELETE tablet` | Bearer | `{revoked:true}` |

Errors are WordPress's `{code, message, data:{status}}`. How the client
reads each code is in the table under "Offline is normal".

## Project layout

| Project | Target | What |
| --- | --- | --- |
| `TheBleedingDeacons.Freedom.Client` | net10.0 | Package **Freedom.Client**: `FreedomClient`, `FreedomApi`, the envelope, the seams |
| `TheBleedingDeacons.Freedom.Models` | net10.0 | The wire types; ships inside Freedom.Client |
| `TheBleedingDeacons.Freedom.Client.Maui` | net10.0-android | Package **Freedom.Client.Maui**: the defaults and `UseFreedom` |
| `TheBleedingDeacons.Freedom.Tests` | net10.0 | xUnit v3: the edges, the envelope, the wire. Drives the coverage badge. |
| `TheBleedingDeacons.Freedom.Specs` | net10.0 | Reqnroll: the behaviour, in the words of this README. A CI gate, not in coverage. |
| `example/Freedom-cli` | net10.0 | The standalone proof |

**The two test projects share one fake server.**
`Tests/Support/FakeFreedomServer.cs` keeps the plugin's rules: versions
from one counter, a complete manifest, secrets sealed per request to the
enrolled key, and the plugin's refusal codes. The Specs project links it
rather than copying it.

## What is not done

- **Keys in the hardware keystore.** Android Keystore RSA with
  OAEP-SHA-1, behind `IFreedomCredentialStore`, is the next step. It is
  the same gap Link has.
- **iOS and Windows.** There is no `ANDROID_ID` on either, and
  WebAuthenticator does not work in an unpackaged Windows app. Register's
  Windows head would need a loopback sign-in and a generated install id,
  under which a reinstall is a new tablet. The seams are there; the
  defaults are not.
- **Removing the callback activity.** The library could ship it with a
  `${applicationId}.freedom` scheme, if .NET Android substitutes manifest
  placeholders in attribute-generated intent filters. That has not been
  tried; until it has, the app writes the one-attribute activity above.
- **Push-triggered sync.** Changes arrive at the next start, not sooner.
- **Play Integrity attestation**, which would make the device identifier
  proof rather than a claim.
- **Typed values.** Everything is a string. The app parses.

## Building and releasing

```bash
dotnet build TheBleedingDeacons.Freedom.sln
dotnet run --project TheBleedingDeacons.Freedom.Tests
dotnet run --project TheBleedingDeacons.Freedom.Specs
dotnet build TheBleedingDeacons.Freedom.Client.Maui -f net10.0-android   # needs the maui-android workload
dotnet format TheBleedingDeacons.Freedom.sln --verify-no-changes
```

Releases are integrity-sharp's:

- `./scripts/release.ps1 0.1.0` bumps both packages' `<Version>`,
  commits, tags `v0.1.0` and pushes.
- The tag runs `release.yml`, which re-runs the gates and publishes
  **Freedom.Client** and **Freedom.Client.Maui** to GitHub Packages and a
  GitHub Release.
- It publishes with the workflow's own `GITHUB_TOKEN`, which asks for
  `packages: write` and `contents: write`, so there is no PAT to keep
  alive. integrity-sharp still uses a `PACKAGES_TOKEN` secret; that
  arrangement predates this one.

To install, add the GitHub Packages feed to `nuget.config` with a token
that has `read:packages`, then reference `Freedom.Client.Maui` (it brings
`Freedom.Client`).

## License

MIT © The Bleeding Deacons
