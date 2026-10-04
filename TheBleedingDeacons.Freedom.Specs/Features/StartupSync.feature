Feature: Every start
  As an app using Freedom
  I want to check my settings on every start and fetch only what changed
  So that a change on the site reaches me without costing every start

  On every start the app asks for the manifest: every key it should hold,
  each with a version, and never a value. It fetches only the keys whose
  version differs from its own, and removes any it holds that the manifest
  no longer lists.

  Rule: Nothing changed costs one small request

    Scenario: A start after a start
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      When the app starts
      Then the start reports UpToDate
      And the start cost one request
      And the device knows how old its configuration is

  Rule: Only stale keys are fetched

    Scenario: One of two values changes
      Given the application has "smtp.host" set to "mail.example.org"
      And the application has "smtp.port" set to "587"
      And the device has signed in
      And the value of "smtp.port" changes to "465"
      When the app starts
      Then the start reports Updated
      And only "smtp.port" was fetched
      And the device holds "smtp.port" as "465"
      And the app was told "smtp.port" changed

  Rule: A key the manifest no longer lists is removed

    The manifest is complete, so absence means removal. There are no
    tombstones to miss.

    Scenario: A value is deleted on the site
      Given the application has "smtp.host" set to "mail.example.org"
      And the application has "legacy.flag" set to "on"
      And the device has signed in
      And "legacy.flag" is removed from the application
      When the app starts
      Then the device does not hold "legacy.flag"
      And the device holds "smtp.host" as "mail.example.org"

  Rule: Versions are compared for difference, not order

    A version is never reused within an application, so any difference is
    a change. That is what makes removing an override work: the default
    the device falls back to carries an older version than the override it
    held — lower, and still a change.

    Scenario: An override is removed and the default comes back
      Given the application has "smtp.host" set to "the default"
      And the device has signed in
      And the device holds "smtp.host" as "its own override" at version 9999
      When the app starts
      Then only "smtp.host" was fetched
      And the device holds "smtp.host" as "the default"

  Rule: The configuration is marked current only when everything applied

    Scenario: The values could not be fetched
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the value of "smtp.host" changes to "smtp.example.org"
      And the server answers every values request with 500
      When the app starts
      Then the start reports ServerError
      And the device holds "smtp.host" as "mail.example.org"
      And the configuration is not marked current at the new manifest

    Scenario: The next start repairs it
      Given the application has "smtp.host" set to "mail.example.org"
      And the device has signed in
      And the value of "smtp.host" changes to "smtp.example.org"
      And the server answers every values request with 500
      And the app has started
      And the server answers values requests normally again
      When the app starts
      Then the start reports Updated
      And the device holds "smtp.host" as "smtp.example.org"
