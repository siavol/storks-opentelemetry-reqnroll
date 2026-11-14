Feature: Checkout

  Scenario: Customer places an order for a regular SKU
    When the customer orders 2 of "SKU-1"
    Then the order total is 50.00

  @demo-failure
  Scenario: Customer places an order for a SKU whose price cannot be fetched
    When the customer orders 2 of "BROKEN-1"
    Then the order total is 50.00
