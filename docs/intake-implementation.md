# Intake implementation note

The intake milestone preserves every delivery under its generated batch key,
including duplicates. It calculates SHA-256 while S3 reads the upload stream and
uses `If-None-Match: *` so an existing key is never silently overwritten.

After storage succeeds, one EF Core `SaveChanges` transaction records either:

- a canonical `Received` batch plus one `Pending` validation/normalization work
  item; or
- a `Duplicate` batch referencing the canonical batch, with no work item.

A PostgreSQL partial unique index permits only one row with a null
`canonical_batch_id` for each `(merchant_id, original_sha256)`. If simultaneous
uploads race, the uniqueness violation is recognized by constraint name; the
loser reloads the winner and persists as a duplicate.

S3 and PostgreSQL cannot commit atomically. Database failure triggers deletion of
the just-written unique object; cleanup failure raises an explicit compensation
exception. A crash after object creation and before database commit can leave an
orphan, while a crash after database commit cannot leave work pointing to a
missing original. A future scheduled reconciler should scan batch-keyed originals
older than a grace period, compare them with PostgreSQL, and quarantine or remove
orphans according to the future retention policy.

Processing artifact failure windows and retry convergence are described in the
[processing implementation note](processing-implementation.md).

The API's 25,000,000-byte HTTP ceiling is a transport safety control. The
documented 10,000,000-byte limit is deliberately not enforced at intake: the
future worker will reject oversized files only after immutable preservation.
