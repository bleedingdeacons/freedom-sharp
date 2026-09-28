@manual @ignore
Feature: On a real tablet
  Acceptance criteria no test host can reach. Walk through them on a
  tablet against the test site before a release that touches sign-in or
  storage — /kick puts a wireless-debug device in reach.

  Rule: The browser comes back to the app

    Scenario: The callback activity catches the redirect
      Given an app whose WebAuthenticatorCallbackActivity names the callback's scheme and host
      When the tablet signs in with a listed Google account
      Then the custom tab closes by itself
      And the app is signed in
      # A mismatch in any of the three places the callback is written is
      # a tab that opens and never comes back, with nothing in any log.

  Rule: ANDROID_ID survives a reinstall under the same signing key

    Scenario: Reinstalled and signed in again
      Given a tablet with an override set on the site
      When the app is uninstalled, reinstalled and signed in again
      Then the admin list shows the same tablet, re-attached
      And the override is still applied

  Rule: A debug build and a release build are different tablets

    Scenario: Both builds on one tablet
      Given a debug build and a release build signed with different keys
      When each signs in
      Then the admin list shows two tablets

  Rule: A factory reset makes a new tablet

    Scenario: Reset and signed in again
      Given a tablet that has been factory reset
      When it signs in
      Then the admin list shows a new tablet, not a re-attachment

  Rule: An invalidated keystore is a sign-in, not a crash

    Scenario: The screen lock was changed
      Given a tablet whose secure storage can no longer be read
      When the app starts
      Then it reports that it is not signed in
      And signing in again restores every value

  Rule: Two apps must not claim one scheme

    Scenario: Another app registers the same callback scheme
      Given a second app claiming the callback scheme
      When the tablet signs in
      Then Android asks which app should open the link
      And a code that reaches the wrong app is useless without the verifier
