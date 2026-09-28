Feature: The only things built in
  As somebody building an app that uses Freedom
  I want to build in nothing but the site, the application and a callback
  So that nothing in the package is a secret

  Rule: Only an https address is somewhere Freedom will talk to

    Scenario: A plain http site is not configured
      Given the device is built to use "http://intergroup.example.org"
      When the app starts
      Then the start reports NotConfigured

    Scenario: A plain http site is allowed for local development
      Given the device is built to use "http://stalwart-dev.local" allowing plain http
      Then the device is configured

  Rule: The REST namespace is appended rather than configured

    Scenario: A site in a subdirectory
      Given the device is built to use "https://intergroup.example.org/amber"
      And the application has "smtp.host" set to "mail.example.org"
      When the device signs in
      Then the requests went to "/amber/wp-json/freedom/v1/"

  Rule: A browser sign-in needs a callback

    Scenario: No callback URI
      Given the device is built with no callback URI
      When the device signs in
      Then the sign-in was not configured
