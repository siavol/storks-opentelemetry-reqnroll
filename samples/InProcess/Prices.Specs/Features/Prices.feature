Feature: Prices

  Scenario: Customer looks up the price of a SKU
    When the customer asks for the price of "SKU-1"
    Then the price is 25.00

  Scenario: Customer looks up the price of another SKU
    When the customer asks for the price of "SKU-2"
    Then the price is 25.00
