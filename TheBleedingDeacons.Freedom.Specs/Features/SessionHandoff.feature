Feature: Handing over a session the app already holds
  As a member using Link
  I want Link to fetch its settings without a second sign-in
  So that I sign in with Google once, not twice

  Link has already signed in through Google, for Fellowship, and holds a
  device token. That token proves what a second trip through the browser
  would: Fellowship minted it after a Google sign-in and re-checks the
  member every time. So Link hands it over instead.

  The library knows nothing about where the token came from. It is a
  bearer string the server checks; an app with no session of its own
  never uses this and signs in through the browser.

  Rule: A signed-in Link device is never sent to the browser twice

    Scenario: A Link session signs the device in with no browser
      Given the application has "betterstack.endpoint" set to "https://in.logs.example"
      When the device signs in with Link's session
      Then the sign-in succeeded
      And the browser was never opened
      And the device holds "betterstack.endpoint" as "https://in.logs.example"

  Rule: An application that does not accept sessions says so

    Scenario: The application has not opted in
      Given the application does not accept a Link session
      When the device signs in with Link's session
      Then the sign-in was refused with "freedom_sessions_not_accepted"
      And the browser was never opened

  Rule: The Freedom device goes when the Link enrolment goes

    The server asks Fellowship, on every request, whether the Link
    enrolment is still live. When it is not, the answer is a 401 — the
    same as a revoked token — and the app hands the new Link session over
    once Link has signed in again.

    Scenario: Link signs out
      Given the device has signed in with Link's session
      And the device's token has been revoked
      When the app starts
      Then the start reports Revoked
      And the device is not signed in
