Feature: Secrets
  As an intergroup whose tablets need an SMTP password and an API key
  I want those sent sealed to each tablet
  So that a logged response or a proxy in the middle does not give them away

  A secret value is sealed per value, on every fetch, to the public key
  the device sent when it signed in: a fresh AES-256-GCM key, wrapped with
  RSA-OAEP to the device. The private half never leaves the device.

  Rule: A secret opens only with this device's key

    Scenario: A secret arrives sealed and is stored opened
      Given the application has a secret "smtp.password" set to "correct horse battery staple"
      When the device signs in
      Then the device holds "smtp.password" as "correct horse battery staple"
      And "smtp.password" is held as a secret

  Rule: A secret that will not open keeps the old value and says so

    It was working a moment ago, and a value the device cannot read is
    worse than one that may be out of date. The server is told, so the
    device shows in red on the admin list; signing in again — a new key —
    cures it.

    Scenario: The device's key no longer matches
      Given the application has a secret "smtp.password" set to "old"
      And the device has signed in
      And secrets are now sealed to a key this device does not hold
      And the value of secret "smtp.password" changes to "new"
      When the app starts
      Then the start reports KeyFault
      And the device holds "smtp.password" as "old"
      And the server was told the device cannot open a secret

    Scenario: Signing in again replaces the key
      Given the application has a secret "smtp.password" set to "old"
      And the device has signed in
      And secrets are now sealed to a key this device does not hold
      And the value of secret "smtp.password" changes to "new"
      And the app has started
      And secrets are sealed to the device's own key again
      When the device signs in again
      Then the device holds "smtp.password" as "new"
