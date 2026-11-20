Feature: Quotes

  Scenario: Customer gets a quote for a regular SKU
    When the customer asks for a quote for "SKU-1"
    Then the quoted price is 25.00

  # Red on purpose: Back.Api answers 500 for this SKU. CI checks that it is the only red scenario.
  @expected-failure
  Scenario: Customer gets a quote for a SKU whose price cannot be fetched
    When the customer asks for a quote for "BROKEN-1"
    Then the quoted price is 25.00
