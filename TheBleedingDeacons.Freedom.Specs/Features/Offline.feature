Feature: Starting without the server
  As an app on a tablet in a church hall with no signal
  I want to start with the settings I already have
  So that the meeting goes ahead

  Offline is normal. A start that cannot reach the site, or reaches it and
  gets an error that is not a refusal, keeps everything and says how old
  it is. Only a refusal clears anything — see Revocation.feature.

  Rule: A failed poll keeps the last known configuration

    Scenario: No network
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the server cannot be reached
      When the app starts
      Then the start reports Offline
      And the device holds "smtp.host" as "mail.example.org"
      And the device knows how old its configuration is

  Rule: A connection lost part-way through a start changes nothing

    The manifest arrives, and the connection drops before the values do —
    typically because the app was sent to the background. Seen on the
    Register tablet on 2026-10-04, where Android reported it in a way the
    client did not recognise and the start threw instead.

    Scenario: The connection drops while the values are fetched
      Given the application has "smtp.host" set to "mail.example.org"
      And the application has "legacy.flag" set to "on"
      And the device has signed in
      And the value of "smtp.host" changes to "smtp.example.org"
      And "legacy.flag" is removed from the application
      And the connection drops during every values request
      When the app starts
      Then the start reports Offline
      And the device holds "smtp.host" as "mail.example.org"
      And the device holds "legacy.flag" as "on"
      And the configuration is not marked current at the new manifest

    Scenario: The next start finishes the job
      Given the application has "smtp.host" set to "mail.example.org"
      And the application has "legacy.flag" set to "on"
      And the device has signed in
      And the value of "smtp.host" changes to "smtp.example.org"
      And "legacy.flag" is removed from the application
      And the connection drops during every values request
      And the app has started
      And the server answers values requests normally again
      When the app starts
      Then the start reports Updated
      And the device holds "smtp.host" as "smtp.example.org"
      And the device does not hold "legacy.flag"

  Rule: A server error is not a refusal

    Scenario Outline: The site is unwell
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the server answers every manifest with <status>
      When the app starts
      Then the start reports ServerError
      And the device holds "smtp.host" as "mail.example.org"
      And the device is signed in

      Examples:
        | status |
        | 500    |
        | 503    |
        | 429    |
        | 403    |
      # A 403 without one of the plugin's refusal codes is a firewall, not
      # the plugin saying no.

  Rule: A suspended application keeps its configuration

    Scenario: The application is disabled on the site
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the application has been disabled
      When the app starts
      Then the start reports Suspended
      And the device holds "smtp.host" as "mail.example.org"

  Rule: A first start with no signal has nothing to keep, and says so

    Scenario: Never signed in
      When the app starts
      Then the start reports NotEnrolled
      And the device holds nothing
