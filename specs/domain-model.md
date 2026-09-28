# Freedom — domain model

The living specification is the set of Reqnroll `.feature` files in
`TheBleedingDeacons.Freedom.Specs/Features`. This is the narrative and the
glossary behind them. Where this and the feature files disagree, the
feature files are what runs, and this is what needs correcting.

## The core idea

An app has three things built in, none of them secret. A device signs in
once with a Google account, and on every start checks a manifest of
versions, fetching only what changed.

```
            first run                         every start
  ┌──────────────────────────┐     ┌──────────────────────────────────┐
  │ browser ─► Google ─►     │     │ manifest (If-None-Match)         │
  │ Fellowship callback ─►   │     │   304 ─► UpToDate                │
  │ Freedom: may this        │     │   200 ─► stale keys ─► values    │
  │ account use this app? ─► │     │          removed keys ─► delete  │
  │ one-time code ─►         │     │          ─► apply, ETag last     │
  │ exchange (+ verifier,    │     │   refusal ─► clear               │
  │  + public key) ─► token  │     │   anything else ─► keep          │
  └──────────────────────────┘     └──────────────────────────────────┘
```

## Ubiquitous language

- **Application.** An app that fetches its settings from Freedom, named by
  its **slug** (`register`, `link`).
- **Callback URI.** Where a browser sign-in returns to the app. A custom
  scheme, written identically in three places.
- **Member account.** A Unity member's own Google account. Always
  accepted, for every application.
- **Common account.** A shared Google account listed for one application:
  what tablets sign in with. Belongs to no member. Always accepted for
  that application.
- **Tablet.** One physical device enrolled for one application; the
  server's row. Called a *device* on the client side, where it may not be
  a tablet at all.
- **Device identifier.** What the device says it is: `ANDROID_ID`, or
  `fellowship:<id>` for a hand-off. Reported, not proved.
- **Device hash.** The server's keyed hash of the application slug and the
  device identifier. What a tablet row is found by.
- **Enrolment.** Signing a device in: proof, then a token.
- **Re-attachment.** An enrolment that finds the device's existing row.
  The device gets a new token and key and keeps its overrides.
- **Hand-off.** An enrolment that presents a session the app already holds
  (Link's) in place of a browser sign-in.
- **Token.** The bearer credential a device holds. `frt_` + 64 hex. The
  server keeps only its HMAC.
- **Keypair.** The device's RSA-2048 pair. The public half goes to the
  server at enrolment; every secret is sealed to it.
- **Key.** A configuration key: `smtp.host`. Lower case, dotted by
  convention.
- **Value.** A string. Always encrypted at rest.
- **Secret.** A value delivered sealed to the device's key.
- **Default.** An application's value for a key.
- **Override.** One tablet's value for a key, replacing the default for
  that tablet only.
- **Effective value.** The override if there is one, otherwise the
  default.
- **Revision.** An application's counter. Only goes up.
- **Version.** The revision a value was written at. Never reused within an
  application. The effective version is the winning row's.
- **Manifest.** Every key a device should hold, each with its version and
  secret flag. Never a value. Complete.
- **ETag.** A hash over the manifest (tablet, keys, versions, secret
  flags). Sent back as `If-None-Match`.
- **Stale.** A key not held, held at a different version, or whose secret
  flag changed.
- **Sync.** What a device does on every start.
- **Verified at.** When the server last confirmed what the device holds.
- **Revocation.** The token is dead. The device may sign in again.
- **Block.** Revocation that also refuses re-attachment.
- **Suspension.** The application is disabled. Devices keep what they
  hold.
- **Key fault.** A secret arrived that the device cannot open.
- **Audience.** Fellowship's name for whoever a browser sign-in is being
  done for: `link` or `freedom`.
- **Context.** Freedom's opaque data carried through Fellowship's browser
  leg: the application and the PKCE challenge.
- **One-time code.** What the browser brings back. Worthless two minutes
  later, once used, and without the verifier.
- **Verifier.** The PKCE secret only the app holds. Its SHA-256 is the
  challenge.

## Locked design decisions

- **Google's console is unchanged.** Freedom signs in through Fellowship's
  existing browser leg and callback, with Fellowship's one client. Adding
  an application needs nothing from Google.
- **Two kinds of account, always accepted, no approval step.** A member's
  own account, or one of the application's common accounts. Admins can
  still revoke, block and remove.
- **Tablets are not Link devices.** Freedom keeps its own table. Link's
  device cap, push token and member-only rule do not apply to a shared
  tablet, and loosening them for tablets would loosen them for Link.
- **A Link session is accepted in place of a second sign-in**, per
  application, and the device goes when the Link enrolment goes.
- **Versions come from a per-application revision and never repeat.**
  Devices compare for difference. Overrides need no client-side
  bookkeeping because of it.
- **The manifest is complete, so absence means removal.** No tombstones.
- **A failed read is a 500, never an empty manifest.** An empty one tells
  every device to delete everything.
- **Secrets are sealed one per value, with the key and version inside the
  seal**, in Fellowship's envelope unchanged: OAEP-SHA-1, because PHP has
  nothing else.
- **Signing in again is the only way to replace a key.** A new key always
  needs a fresh Google proof, so a stolen token alone yields only plain
  values.
- **Only a refusal clears anything**, and clearing on refusal is the
  default.
- **The ETag is saved last**, and only when every stale key applied.
- **HTTPS only. The REST namespace is appended to the site's address,
  never configured.**
- **freedom-sharp stands on its own.** It has no dependency on Link or on
  Fellowship's client. A hand-off token is opaque to it.

## Assumptions still open to change

- **`ANDROID_ID` as the device identifier.** A claim, not proof. Play
  Integrity would make it proof.
- **No approval step.** Any listed or member account enrols at once. If a
  common account's password leaks, every device signed in with it is
  admitted until the account is removed.
- **Wiping on refusal by default.** Right for secrets; debatable for an
  app that would rather limp along.
- **Strings only.** No typed values, no schema.
- **Google as the provider in practice.** Fellowship offers Microsoft and
  Facebook for a browser sign-in too; nothing has been tried with them.
- **Polling at start only.** No push when a value changes.
- **Identity on iOS and Windows.** Not decided. See "What is not done" in
  the README.
- **A cap of 100 tablets per application.** Generous for an intergroup,
  and a guess.
