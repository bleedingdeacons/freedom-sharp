Feature: Signing a device in
  As somebody setting up a tablet
  I want to sign it in once with a Google account
  So that it can fetch its settings instead of having them built in

  A device signs in through the browser: the app opens the provider's
  page, Google sends the browser back to Fellowship, Fellowship asks
  Freedom whether this account may use the application, and the browser
  comes back to the app with a one-time code. The app spends the code,
  with the PKCE verifier only it holds, for a token.

  Two kinds of account are always accepted: a Unity member's own, and one
  of the application's common tablet accounts. There is no approval step.

  Rule: Changing your mind is not a failure

    Scenario: Closing the browser is a cancel with nothing to say
      Given the user will close the browser
      When the device signs in
      Then the sign-in was cancelled
      And the device is not signed in

  Rule: A refusal carries the server's own words

    Scenario: An account that may not use the application is told so
      Given the server will refuse the sign-in in the browser with "not_authorised"
      When the device signs in
      Then the sign-in was refused with "not_authorised"
      And the message says "Google account"

    Scenario: A refusal at the exchange carries the plugin's code
      Given the server will refuse the sign-in with "freedom_not_authorised"
      When the device signs in
      Then the sign-in was refused with "freedom_not_authorised"

  Rule: Signing in fetches the configuration straight away

    Scenario: A new device comes away with its settings
      Given the application has "smtp.host" set to "mail.example.org"
      When the device signs in
      Then the sign-in succeeded
      And the device holds "smtp.host" as "mail.example.org"

  Rule: Signing in again re-attaches the same device

    A device that is reinstalled, or whose token was revoked, signs in
    again and lands on its old row: its overrides survive, and it gets a
    new token and a new key.

    Scenario: A second sign-in is a re-attachment with a fresh key
      Given the device has signed in
      When the device signs in again
      Then the sign-in succeeded
      And the device was re-attached
      And the device sent a new public key

  Rule: The server cannot be reached is a failure, not a refusal

    Scenario: No network while signing in
      Given the server cannot be reached
      When the device signs in
      Then the sign-in failed
      And the device is not signed in
