# ADR 0003: Use a PostgreSQL-backed work queue

## Status

Accepted

## Context

The first slice needs durable asynchronous processing, but its expected scale
does not justify a separate broker or streaming platform.

## Decision

Store queued work with operational batch state in PostgreSQL. One background
worker will transactionally claim available work using PostgreSQL locking and a
recoverable lease, then update attempts and outcomes idempotently.

## Consequences

Enqueueing can share a transaction with batch creation, reducing coordination
points and keeping local operations simple. Queue polling, indexes, lock duration,
retry policy, poison work, and lease recovery must be designed and tested. If
measured throughput or fan-out later exceeds this design, a broker can be
introduced from evidence rather than assumption.
