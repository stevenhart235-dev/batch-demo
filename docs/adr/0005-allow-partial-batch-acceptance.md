# ADR 0005: Allow partial batch acceptance

## Status

Accepted

## Context

A structurally readable batch may contain isolated invalid instructions. Rejecting
all rows would discard valid merchant intent and give less useful feedback.

## Decision

Validate rows independently after file-level validation. Emit valid rows to the
accepted artifact and invalid rows to the rejected artifact. Use `Ready` when all
rows are accepted, `ReadyWithExceptions` when both accepted and rejected rows
exist, and `Rejected` for a structurally invalid file or zero accepted rows.

## Consequences

Every input row must be traceable and counted exactly once. Consumers must inspect
batch status and rejection details rather than treating processing as all-or-none.
File-level failures remain atomic, and no processor submission behavior is
implied.
