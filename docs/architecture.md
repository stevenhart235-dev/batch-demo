# Architecture

## Initial components

- **ASP.NET Core application/API:** accepts one CSV upload, creates the batch,
  and serves batch status and results.
- **PostgreSQL:** owns batch/row operational state and queued work.
- **S3-compatible object storage:** owns immutable originals and generated
  normalized artifacts.
- **Background worker:** claims queued batches, reads originals, validates and
  normalizes rows, writes artifacts, and records outcomes.
- **Docker Compose:** supplies local PostgreSQL and S3-compatible infrastructure.

The application and worker may begin in one solution, but their responsibilities
and execution paths remain separate.

The implemented solution separates Domain, Application, Infrastructure, API,
and Worker projects. The worker hosts polling and lease identity; application
services orchestrate processing; infrastructure implements PostgreSQL leasing,
CsvHelper parsing, and S3-compatible artifact access.

## Implemented processing boundary

Workers claim one pending or expired-leased item in a short PostgreSQL
`FOR UPDATE SKIP LOCKED` transaction. The transaction commits before object I/O.
Processing is at-least-once, so artifacts use deterministic final keys and
byte-checked create-if-absent publication. Database completion references only a
fully published artifact set. See the [processing implementation note](processing-implementation.md).

## Implemented intake boundary

The API accepts multipart uploads and delegates to an application intake service.
Infrastructure streams exact bytes to S3-compatible storage through an
application-owned abstraction, calculates SHA-256 during that stream, and then
persists the batch and optional work item through EF Core. PostgreSQL's partial
unique index on `(merchant_id, original_sha256)` where `canonical_batch_id` is
null makes canonical-delivery selection deterministic under concurrency.

Object storage is completed first. A database failure triggers best-effort object
deletion; a failed cleanup is surfaced rather than hidden. A process crash after
object creation but before database commit can still leave an orphan. A future
reconciler must compare batch-keyed objects and database rows after a grace period.
See the [intake implementation note](intake-implementation.md).

## Processing flow

1. The API assigns a batch ID and stores the uploaded bytes under a unique,
   write-once object key.
2. After storage succeeds, the API records the source object identity and queues
   the batch in the same PostgreSQL transaction.
3. The worker claims work with a database lease/lock and streams the source
   object.
4. Each row becomes either canonical payment intent or a rejection containing
   source row number, merchant reference when present, and reason codes.
5. The worker writes normalized valid-row and rejection artifacts to object
   storage, then records their identities, counts, and terminal batch status in
   PostgreSQL.
6. The API reads PostgreSQL for status and summary and exposes result details or
   artifact references for display.

## Ownership and boundaries

PostgreSQL is authoritative for lifecycle state, work ownership, attempts,
counts, and object references. Object storage is authoritative for uploaded and
generated file bytes. Database records never substitute for artifact storage,
and object listings never substitute for workflow state.

The canonical model represents integer-minor-unit payment intent and retains
source provenance. It must not contain raw card data, bank-account details, or
processor credentials. Gold output represents validated intent only; a future
adapter owns processor-specific mapping and transport. Exact input, artifact,
and status contracts are defined in the [data contract](data-contract.md), with
orchestration rules in the [processing flow](processing-flow.md).

## Reliability baseline

- Use unique object keys and retain source checksum, size, and media type.
- Make claiming and state transitions transactional; recover abandoned leases.
- Make processing idempotent by batch ID and artifact version/key.
- Preserve row provenance and stable machine-readable rejection codes.
- Mark a batch complete only after artifact writes and state updates are safely
  correlated; define compensation for partial failures during implementation.

## Discovery assumptions

The initial CSV and artifact rules are provisional but concrete for the first
slice. Retention, deletion, merchant identity, concurrency behavior for identical
uploads, and operational retry/lease settings still require confirmation.
