# Portal implementation

The operator portal is ordinary HTML, CSS, and JavaScript hosted by the existing
ASP.NET Core API. This keeps the local demo to the existing API and worker
processes and adds no frontend framework or build tool.

## Workflow

The portal submits the existing multipart upload contract, displays batch
identity and status, and polls every 1.5 seconds. Polling stops for `Ready`,
`ReadyWithExceptions`, `Rejected`, `ProcessingFailed`, and `Duplicate`. Three
consecutive network/API failures stop with an actionable error. Terminal results
show counts, tables, summary details, file-level reasons, and a reset action. A
duplicate links to the canonical batch when its ID is available.

## Result boundary and safety

`GET /api/batches/{batchId}/results` accepts only a batch ID. The application
loads the database record and reads its accepted, rejected, and summary keys; it
is not an object-download proxy. The response replaces each opaque payment
credential reference with `credentialReferencePresent`. Object-store credentials
remain server-side.

Before returning rejected `originalRowContent`, the API replaces the contracted
credential column with `[credential redacted]`; file-level captured content stays
only in the protected artifact. The browser renders the remaining row text through
DOM `textContent`. Null row numbers and references render as an em dash.
Structural reasons remain visible while their rejected-row count stays zero as
defined by the data contract.
