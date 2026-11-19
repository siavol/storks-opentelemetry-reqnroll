# Coding Standards

## Comments

A comment earns its place in one of two cases:

- **Public contract**: the doc comment of a public API member, stating what it promises the caller.
- **Non-obvious goal**: the _why_ the code cannot show (the purpose, constraint or gotcha behind it).

Write the goal in one short sentence. Leave the mechanics (each return case, every branch, step-by-step flow) to the code.

A comment states intent, and intent outlives the implementation. When a change edits code and its comment together, the comment describes mechanics or the code does too much: refactor until a one-line intent comment stays true, by rewriting the comment, splitting the code, or both.
