Feature: Quotes

  Scenario: Customer gets a quote for a basket with a bulk discount
    Given the basket has
      | sku   | quantity |
      | SKU-1 | 10       |
      | SKU-2 | 1        |
    When the customer asks for a quote
    Then the quote total is 265.00

  Scenario: Customer asks for a quote for a basket with an unknown SKU
    Given the basket has
      | sku    | quantity |
      | SKU-1  | 2        |
      | NOPE-1 | 1        |
    When the customer asks for a quote
    Then the quote is rejected for "NOPE-1"
