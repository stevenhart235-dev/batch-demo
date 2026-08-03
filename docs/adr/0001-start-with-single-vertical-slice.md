# ADR 0001: Start with a single vertical slice

## Status

Accepted

## Context

The lab must discover ingestion, durability, validation, normalization, and
result-display risks without committing early to a broad payment platform.

## Decision

Implement one end-to-end flow: upload one merchant CSV, preserve it, normalize
valid rows, isolate invalid rows, and display the batch result. Keep canonical
gold output processor-neutral behind a future adapter boundary.

## Consequences

The project gets early executable feedback with few components and explicit
boundaries. Authentication, alternate ingestion, submission, settlement,
reconciliation, streaming, and production concerns are deferred. Some contracts
will intentionally evolve as the slice supplies evidence.
