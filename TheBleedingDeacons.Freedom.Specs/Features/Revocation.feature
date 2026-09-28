Feature: Losing access
  As an intergroup that has taken a tablet out of service
  I want it to let go of the secrets it was sent
  So that a tablet left in a cupboard is not still holding an SMTP password

  Only a refusal clears anything: a revoked token, an account that may no
  longer use the application, a blocked device. What it clears is the
  device's copy. It cannot un-know a secret it already had — rotate the
  credential itself for that.

  Rule: Only a refusal clears configuration

    Scenario: The token was revoked
      Given the application has a secret "smtp.password" set to "secret"
      And the device has signed in
      And the device's token has been revoked
      When the app starts
      Then the start reports Revoked
      And the device holds nothing
      And the device is not signed in
      And the app was told "smtp.password" was removed

    Scenario: The account may no longer use the application
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the account may no longer use the application
      When the app starts
      Then the start reports NotAuthorised
      And the device holds nothing

    Scenario: The device was blocked
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the device has been blocked
      When the app starts
      Then the start reports NotAuthorised
      And the device holds nothing

    Scenario: An app that would rather keep what it has on a refusal
      Given the device does not clear on refusal
      And the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the device's token has been revoked
      When the app starts
      Then the start reports Revoked
      And the device holds "smtp.host" as "mail.example.org"

  Rule: Signing out clears both stores

    Scenario: Signing out
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      When the device signs out
      Then the device holds nothing
      And the device is not signed in
      And the server revoked the token
