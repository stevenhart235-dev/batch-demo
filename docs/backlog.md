# Backlog

## First vertical slice

- [x] Define the canonical payment, rejection, batch status, and reason-code
  contracts.
- [x] Define the merchant CSV schema and parsing/validation policy.
- [x] Create Docker Compose services for PostgreSQL and S3-compatible storage.
- [x] Add an upload endpoint with an HTTP safety ceiling and immutable source
  storage; processing size/type validation remains worker work.
- [x] Persist intake batch state and enqueue work transactionally in PostgreSQL.
- [x] Implement one worker with safe claiming, expired-lease recovery, bounded
  retry, and idempotent finalization behavior.
- [x] Produce normalized valid-row, rejected-row, and summary artifacts with
  provenance.
- [x] Add batch intake and processing results to `GET /api/batches/{batchId}`.
- [ ] Add a minimal result display.
- [x] Test valid, invalid, duplicate, retry, concurrency, lease recovery, and
  partial-artifact finalization paths.
- [x] Document local startup and an end-to-end demo using the sample CSV.

## Decisions to refine

- [x] Set provisional CSV columns, encoding, delimiter, amount precision, and
  row limits.
- [x] Set provisional currencies and batch-local duplicate-reference scope.
- [x] Choose provisional JSONL/JSON artifact formats and schemas.
- [ ] Define retention, deletion, and sensitive-data handling policies.
- [x] Set configurable lease/polling values, three attempts by default, and
  `ProcessingFailed` for exhausted infrastructure failures.
- [ ] Confirm merchant identity and concurrent identical-upload semantics.
- [x] Protect concurrent identical delivery with a PostgreSQL partial unique
  index and deterministic duplicate recovery for the provisional API merchant ID.
- [ ] Implement orphan-object reconciliation for intake crash windows.

## Later—not in the initial slice

Authentication, SFTP, processor adapters/submission, settlement, reconciliation,
streaming infrastructure, Ceph operations, and production deployment.
