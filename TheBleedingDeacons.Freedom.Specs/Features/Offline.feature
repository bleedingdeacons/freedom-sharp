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
