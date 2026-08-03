# Processing implementation note

## Leasing and delivery

The worker polls PostgreSQL and claims one available `Pending` item—or an expired
`Leased` item—in a short `FOR UPDATE SKIP LOCKED` transaction. Claiming sets the
lease owner/expiry, increments `AttemptCount`, and records the first processing
start time. The transaction commits before S3 reads or CSV work. This is an
at-least-once delivery model; shutdown cancellation leaves recovery to lease
expiry.

Polling interval, lease duration, and maximum attempts are configurable. Defaults
are two seconds, sixty seconds, and three attempts. A transient failure records a
bounded safe error and returns work to `Pending`. Exhaustion marks the work
`Failed` and the batch `ProcessingFailed`. A determinable data rejection marks
work `Completed` and the batch `Rejected`.

## Validation and artifacts

CsvHelper handles RFC-compatible quoting, escaped quotes, and embedded newlines.
The parser enforces the file and row rules in `data-contract.md`, including the
10,000,000-byte processing limit. Source row numbers identify each logical
record's starting physical line. Exact original bytes remain in the immutable
source object; artifact `originalRowContent` is the decoded logical CSV record
without its terminal record separator.

Accepted and rejected JSONL and summary JSON publish directly to deterministic
batch final keys using create-if-absent semantics. If a retry finds an artifact,
its bytes must exactly match the deterministic output or processing fails. The
summary `artifactGeneratedAt` timestamp uses the stable first processing-start
timestamp so retry output remains deterministic. It is deliberately not named
`completedAt`; the batch API's `processingCompletedAt` is the truthful database
finalization timestamp.

## Failure windows

If publication stops partway, PostgreSQL remains incomplete and a retry converges
on the same keys. If all objects exist but database completion fails, retry
verifies their bytes and finalizes the same batch. PostgreSQL completion is one
transaction updating aggregate batch results and the work item. The remaining
operational risk is stale partial artifact sets for work that ultimately exhausts
retries; future retention/reconciliation policy must decide their cleanup.

If a worker disappears during its third (maximum) attempt, the expired lease is
not reclaimed for a fourth attempt. The next poll locks it safely, marks the work
`Failed`, records a bounded error, and marks the batch `ProcessingFailed`.

## Demonstration sample accounting

The ten sample rows yield six accepted and four rejected records:

- `DEMO-1001`, `DEMO-1002`, `DEMO-1003`, `DEMO-1007`, the first `DEMO-1008`,
  and `DEMO-1010` are valid. `DEMO-1002` also proves `eur` normalizes to `EUR`.
- `DEMO-1004` is rejected with `UnsupportedCurrency` for `USQ`.
- The row after `DEMO-1004` is rejected with `MissingMerchantReference`.
- `DEMO-1006` is rejected with `InvalidAmount` for `twelve`.
- The second `DEMO-1008` is rejected with `DuplicateMerchantReference`.
