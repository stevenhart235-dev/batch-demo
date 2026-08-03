# ADR 0004: Use processor-neutral canonical payment intent

## Status

Accepted

## Context

Validated merchant instructions need a stable gold representation, but the lab
does not yet target a processor, format, or submission transport.

## Decision

Represent accepted rows as processor-neutral canonical payment intent. Store
money as integer minor units, retain merchant-supplied intent and platform
provenance, and use opaque credential and authorization references. Exclude card
numbers, bank-account details, and processor credentials. A future adapter will
map canonical intent to a processor contract.

## Consequences

Validation and audit behavior can stabilize independently of downstream
integrations. Currency exponent handling must be explicit; the initial supported
currencies all use two minor-unit digits. Processor-specific requirements are
not evidence for changing the canonical model unless they express genuine
merchant intent.
