# Data contract

This contract is provisional for the first vertical slice. JSON property names
are case-sensitive camelCase; timestamps use UTC RFC 3339 text; dates use ISO
`YYYY-MM-DD`; absent optional JSON values are `null`.

## CSV envelope

- UTF-8, comma-delimited, with exactly one header row; a UTF-8 BOM is allowed.
- Maximum stored file size: 10 MB (10,000,000 bytes); maximum 10,000 nonblank
  data rows. Blank lines are ignored and do not receive source row numbers.
- Header names are matched exactly after trimming surrounding whitespace.
- CSV quoting follows RFC 4180 conventions. The raw decoded CSV record, including
  unexpected fields, is retained as `originalRowContent`.
- Required columns missing from the header reject the file. Unexpected columns
  are retained under `sourceMetadata` but never affect canonical processing.
- Individual field errors reject only that row. A row with multiple errors emits
  all deterministically detected reasons.

`sourceRowNumber` is the one-based physical CSV line on which the record begins;
the header is line 1. Multiline quoted fields are permitted, so numbers can have
gaps and represent a record's starting line.

## CSV schema

| Column | Presence | Merchant rule | Canonical mapping |
|---|---|---|---|
| `merchant_reference` | Required column and nonblank value | Trimmed, 1–100 characters; unique within the batch | `merchantReference` |
| `operation` | Required column and nonblank value | Case-insensitive `Purchase` or `Refund`; normalized to that spelling | `operation` |
| `amount` | Required column and nonblank value | Positive base-10 decimal, no sign/group separators/exponent, at most two fractional digits, and representable as signed 64-bit minor units | `amountMinor` |
| `currency` | Required column and nonblank value | Trimmed and uppercased; one of `USD`, `EUR`, `GBP`, `CAD` | `currency` |
| `payment_credential_reference` | Required column and nonblank value | Opaque 1–200-character reference; never a PAN or bank-account value | `paymentCredentialReference` |
| `original_authorization_reference` | Optional column | Required for `Refund`, omitted/blank for `Purchase`; opaque 1–200-character reference | `originalAuthorizationReference` |
| `requested_execution_date` | Optional column | Blank or a real ISO date (`YYYY-MM-DD`); no scheduling semantics are implied yet | `requestedExecutionDate` |

All supported currencies currently have two fractional digits, so `24.95` maps
to `2495`; `24` maps to `2400`. Duplicate detection uses the trimmed,
case-sensitive merchant reference. Empty optional columns map to `null`.

## Canonical payment-intent schema

| Field | Type | Source | Meaning |
|---|---|---|---|
| `batchId` | UUID string | Platform | Batch assigned to this delivery |
| `sourceRowNumber` | Integer | Platform | Starting physical line of the CSV record |
| `merchantReference` | String | Merchant | Batch-local instruction identifier |
| `operation` | `Purchase` or `Refund` | Merchant, normalized | Requested operation |
| `amountMinor` | 64-bit integer | Merchant, normalized | Amount in currency minor units |
| `currency` | String | Merchant, normalized | Uppercase supported currency |
| `paymentCredentialReference` | String | Merchant | Opaque credential reference |
| `originalAuthorizationReference` | String or null | Merchant | Prior authorization reference for a refund |
| `requestedExecutionDate` | Date string or null | Merchant | Requested date, without processor semantics |
| `ingestedAt` | Timestamp string | Platform | Time the platform accepted the upload |
| `originalRowContent` | String | Platform capture | Exact decoded logical CSV record before normalization |
| `sourceMetadata` | Object | Merchant passthrough | Unexpected column names and decoded values |

Canonical intent contains no card number, bank-account detail, processor
credential, processor format, or transport instruction.

## Rejection artifact schema

The rejected artifact contains one JSON object per rejected row. For a file-level
failure it contains one object with `sourceRowNumber` and `merchantReference` set
to `null` and the available header/content in `originalRowContent`; this is an
empty string when invalid encoding prevents decoded content.

| Field | Type | Meaning |
|---|---|---|
| `batchId` | UUID string | Delivery batch |
| `sourceRowNumber` | Integer or null | Starting source line; null for file failures |
| `merchantReference` | String or null | Trimmed reference when available |
| `originalRowContent` | String | Original decoded row or available file-level content |
| `sourceMetadata` | Object | Unexpected decoded CSV fields, or empty object |
| `reasons` | Nonempty array | Stable `code`, readable `message`, and optional `field` objects |
| `ingestedAt` | Timestamp string | Upload ingestion time |

### Initial rejection codes

| Code | Level | Meaning |
|---|---|---|
| `InvalidEncoding` | File | Input is not valid UTF-8 |
| `FileTooLarge` | File | Input exceeds 10,000,000 bytes |
| `TooManyRows` | File | Input exceeds 10,000 nonblank data rows |
| `MalformedCsv` | File | CSV quoting or record structure cannot be parsed |
| `MissingRequiredColumn` | File | One or more required headers are absent |
| `MissingMerchantReference` | Row | Merchant reference is blank |
| `DuplicateMerchantReference` | Row | Reference repeats an earlier row; the first occurrence remains eligible |
| `InvalidAmount` | Row | Amount violates decimal, positivity, precision, or range rules |
| `UnsupportedCurrency` | Row | Normalized currency is not supported |
| `InvalidOperation` | Row | Operation is missing or unsupported |
| `InvalidExecutionDate` | Row | Nonblank date is not a real ISO date |
| `MissingCredentialReference` | Row | Credential reference is blank |
| `InvalidOriginalAuthorizationReference` | Row | Refund reference is missing/invalid, or one is supplied for a purchase |

## Batch-summary schema

| Field | Type | Meaning |
|---|---|---|
| `batchId` | UUID string | Delivery batch |
| `merchantId` | String | Platform-known merchant identity, not sourced from CSV |
| `status` | String | `Ready`, `ReadyWithExceptions`, or `Rejected` |
| `originalFileName` | String | Sanitized display name |
| `originalSha256` | String | Lowercase 64-character SHA-256 hex digest of stored bytes |
| `ingestedAt` | Timestamp string | Upload ingestion time |
| `artifactGeneratedAt` | Timestamp string | Stable artifact-generation timestamp; uses the first processing-start time so retries serialize identical bytes |
| `totalRows` | Integer | Nonblank data rows observed; zero if structure prevents counting |
| `acceptedRows` | Integer | Rows emitted to accepted JSONL |
| `rejectedRows` | Integer | Row-level rejections; file-level rejection does not increment this count |
| `fileRejectionReasons` | Array | Empty unless file validation fails; uses rejection reason objects |
| `artifacts` | Object | Object keys for `original`, `accepted`, `rejected`, and `summary` |

Counts satisfy `totalRows = acceptedRows + rejectedRows` when file parsing reaches
row validation. Empty accepted/rejected artifacts are valid.

## Examples

Accepted JSONL record (shown wrapped for readability; the artifact uses one line):

```json
{"batchId":"6b73b3aa-3e6e-4bd3-90fd-538e022bc43a","sourceRowNumber":2,"merchantReference":"DEMO-1001","operation":"Purchase","amountMinor":2495,"currency":"USD","paymentCredentialReference":"tok_demo_alpha","originalAuthorizationReference":null,"requestedExecutionDate":"2026-08-05","ingestedAt":"2026-08-03T15:04:05Z","originalRowContent":"DEMO-1001,Purchase,24.95,USD,tok_demo_alpha,,2026-08-05","sourceMetadata":{}}
```

Rejected JSONL record:

```json
{"batchId":"6b73b3aa-3e6e-4bd3-90fd-538e022bc43a","sourceRowNumber":5,"merchantReference":"DEMO-1004","originalRowContent":"DEMO-1004,Purchase,72.10,USQ,tok_demo_delta,,","sourceMetadata":{},"reasons":[{"code":"UnsupportedCurrency","message":"Currency 'USQ' is not supported.","field":"currency"}],"ingestedAt":"2026-08-03T15:04:05Z"}
```

Summary JSON:

```json
{
  "batchId": "6b73b3aa-3e6e-4bd3-90fd-538e022bc43a",
  "merchantId": "merchant_demo",
  "status": "ReadyWithExceptions",
  "originalFileName": "demo-merchant-batch.csv",
  "originalSha256": "8d9e86b814af059ecc4feb187d7c232db6f58d6adfc7000e593fcfbf384f0852",
  "ingestedAt": "2026-08-03T15:04:05Z",
  "artifactGeneratedAt": "2026-08-03T15:04:05Z",
  "totalRows": 10,
  "acceptedRows": 6,
  "rejectedRows": 4,
  "fileRejectionReasons": [],
  "artifacts": {
    "original": "merchants/merchant_demo/batches/6b73b3aa-3e6e-4bd3-90fd-538e022bc43a/original/demo-merchant-batch.csv",
    "accepted": "merchants/merchant_demo/batches/6b73b3aa-3e6e-4bd3-90fd-538e022bc43a/results/accepted.jsonl",
    "rejected": "merchants/merchant_demo/batches/6b73b3aa-3e6e-4bd3-90fd-538e022bc43a/results/rejected.jsonl",
    "summary": "merchants/merchant_demo/batches/6b73b3aa-3e6e-4bd3-90fd-538e022bc43a/results/summary.json"
  }
}
```

## Explicit provisional assumptions

- Merchant identity is supplied by trusted API context because authentication is
  outside this slice; it is not a CSV column.
- `Purchase` and `Refund` are the only initial operations. A refund requires an
  opaque original authorization reference; this is validation, not submission.
- Dates are validated but not scheduled, timezone-adjusted, or submitted.
- Duplicate merchant-reference comparison is trimmed and case-sensitive, and the
  first occurrence wins.
- File SHA-256 is computed over exact uploaded bytes. Identical-delivery scope is
  the same merchant and checksum, independent of filename.
- Ten MB means decimal 10,000,000 bytes. Artifact retention and encryption/access
  policy remain to be decided.
