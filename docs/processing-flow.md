# Processing flow

## Upload-to-result sequence

1. Resolve the provisional trusted `merchantId`, generate a UUID `batchId`, and
   capture one `ingestedAt` timestamp.
2. Stream the upload once to a unique immutable original object while computing
   SHA-256 over the exact bytes. Record size, sanitized filename, object key, and
   checksum in PostgreSQL. Parsing never begins before this succeeds.
3. In the same merchant scope, compare the checksum with previously canonical
   stored deliveries. If identical, mark this delivery `Duplicate`, link its
   `duplicateOfBatchId`, and do not enqueue or create result artifacts.
4. Transactionally move a unique delivery from `OriginalStored` to `Queued` and
   create its PostgreSQL work item.
5. The worker transactionally leases the item, changes the batch to `Processing`,
   and reads the immutable original.
6. Validate file size, UTF-8, CSV structure, required headers, and row limit. A
   failure bypasses row normalization and produces a rejected batch.
7. Validate each row independently. Preserve its starting source line and
   original decoded logical record. Normalize valid rows to canonical intent;
   emit invalid rows with all detected stable reasons.
8. Write deterministic accepted and rejected JSONL artifacts, including empty
   files, then write the JSON summary. Publish them under batch-specific keys.
9. In PostgreSQL, record artifact keys, counts, completion time, and terminal
   status: `Ready`, `ReadyWithExceptions`, or `Rejected`.
10. The API displays status and summary and makes result details/artifact
    references available. It performs no processor submission.

## Batch states

```text
Receiving -> OriginalStored -> Queued -> Processing -> Ready
                                              |      -> ReadyWithExceptions
                                              |      -> Rejected
                    -> Duplicate
```

- `Receiving`: batch ID exists and the original is being stored.
- `OriginalStored`: immutable bytes and SHA-256 are durable.
- `Queued`: unique delivery has durable PostgreSQL work.
- `Processing`: represented operationally by `processing_started_at` while a
  work item holds a recoverable `Leased` state; the batch remains `Received`
  until its terminal result.
- `Ready`: at least one accepted row and no rejected rows.
- `ReadyWithExceptions`: at least one accepted and one rejected row.
- `Rejected`: file-level validation failed or zero rows were accepted.
- `Duplicate`: identical bytes already exist for the same merchant; terminal and
  linked to the original batch. This delivery has no summary/result artifacts.

Infrastructure failures return work to `Pending` until attempts are exhausted.
Result artifacts use deterministic create-if-absent keys and byte verification;
database completion records them only after the entire set is known to exist.

Implemented queue states are `Pending`, `Leased`, `Completed`, and `Failed`.
Expired leases are reclaimed with `FOR UPDATE SKIP LOCKED`; attempts increment on
each successful claim. The default maximum is three. Retryable failures return
to `Pending`; exhaustion marks work `Failed` and the batch `ProcessingFailed`.
Business/data rejection instead completes the work successfully.
An expired third-attempt lease is terminalized on the next polling pass rather
than reclaimed for a fourth attempt.

## File-level and row-level behavior

File-level failures include excess size/rows, invalid UTF-8, malformed CSV, and
missing required columns. They emit empty accepted JSONL, a rejected JSONL file
containing one file-level rejection record, and a summary with status `Rejected`.
No row is canonicalized.

Field validation begins only after file validation. Each nonblank row is emitted
exactly once to accepted or rejected JSONL. Mixed results are
`ReadyWithExceptions`; all-valid results are `Ready`; no valid rows is
`Rejected`. Blank lines are ignored. Unexpected columns are trace metadata only.

## Idempotency and duplicate delivery

- Delivery duplicate key: `(merchantId, originalSha256)`. Filename and upload
  time do not participate.
- The first durably stored delivery is canonical. A later identical delivery gets
  its own batch ID/original object for audit, becomes `Duplicate`, and points to
  the first batch; it is never processed again.
- Concurrent matching deliveries require a PostgreSQL uniqueness rule/transaction
  so exactly one is canonical.
- Worker idempotency key is `batchId`. Each source row is identified by
  `(batchId, sourceRowNumber)`.
- Artifact final keys are deterministic by batch. Database completion references
  only a complete, correlated artifact set.

## Portal result read

The portal polls `GET /api/batches/{batchId}` until any terminal status, then
uses `GET /api/batches/{batchId}/results`. The API resolves artifact keys only
from the persisted batch and returns a projection without credential-reference
values. `ProcessingFailed` and `Duplicate` return terminal metadata without
requiring artifacts; a duplicate includes its canonical batch ID when known. A
received batch or a missing/unreadable terminal artifact set returns `409 Conflict`.

## Artifact naming

Keys use sanitized identifiers and never merchant-provided path segments except
the sanitized display filename:

```text
merchants/{merchantId}/batches/{batchId}/original/{sanitizedFileName}
merchants/{merchantId}/batches/{batchId}/results/accepted.jsonl
merchants/{merchantId}/batches/{batchId}/results/rejected.jsonl
merchants/{merchantId}/batches/{batchId}/results/summary.json
```

Object keys are unique per batch and treated as write-once after publication.
Content types are `text/csv; charset=utf-8`, `application/x-ndjson`, and
`application/json`, respectively.
