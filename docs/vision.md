# Vision

## Purpose

Prove the smallest useful merchant batch-payment intake flow while keeping data
ownership and future integration boundaries explicit.

## First outcome

For one uploaded merchant CSV, the system can:

1. Preserve the exact original as an immutable artifact.
2. Track the batch and its queued processing durably.
3. Validate and normalize acceptable rows into canonical payment intent.
4. Isolate rejected rows with actionable reasons.
5. Display batch status and valid/invalid row counts and details.

## Success criteria

- Every accepted upload has a durable batch identifier and immutable source.
- Processing is restart-safe and does not silently lose or duplicate work.
- Every input row is accounted for as valid or invalid.
- Gold output is processor-neutral and can feed a future adapter.
- The entire slice runs locally with Docker Compose infrastructure.

## Non-goals

This lab does not cover streaming platforms, Ceph operations, SFTP,
authentication, processor submission, settlement, reconciliation, or production
deployment. “Submission-ready” does not imply a processor format or transport.
