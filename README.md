# batch-demo

`batch-demo` is a discovery lab for a lightweight merchant batch-payment intake
pipeline. It demonstrates clear ownership of source artifacts, canonical payment
intent, validation failures, and durable asynchronous work without prematurely
designing downstream payment processing.

## First vertical slice

Upload one merchant CSV, preserve its immutable original, normalize valid rows
into a canonical payment model, isolate invalid rows, and display the batch
result. The normalized (gold) output expresses validated canonical intent behind
a future adapter boundary; it is not a processor-specific submission file.

## Explicit non-goals

- Redpanda, Kafka, or another event-streaming platform
- Operating Ceph or building an object-storage control plane
- SFTP ingestion
- Authentication or multi-tenant authorization
- Processor submission or a processor-specific format or transport
- Settlement, reconciliation, or production deployment

## Proposed initial components

- An ASP.NET Core application/API for upload and batch-result queries
- PostgreSQL for operational state and the durable work queue
- S3-compatible object storage for immutable originals and normalized artifacts
- One background worker for validation and normalization
- Docker Compose for local PostgreSQL and object-storage infrastructure

## Executable vertical slice

The implemented milestone exposes `POST /api/batches` and
`GET /api/batches/{batchId}`. Intake streams exact uploaded bytes to immutable
S3-compatible storage while calculating SHA-256, then transactionally records a
`Received` batch and one `Pending` work item in PostgreSQL. An identical file for
the same merchant is separately preserved and recorded as `Duplicate`, points to
the first canonical batch, and receives no work item.

The worker now claims PostgreSQL work with a recoverable lease, validates and
normalizes CSV rows, publishes accepted/rejected JSONL plus summary JSON, and
persists final counts and status. Processor submission remains out of scope.

## Local prerequisites

- .NET SDK 10.0.302 or a compatible .NET 10 SDK selected by `global.json`
- Docker Engine with Docker Compose v2
- PowerShell 7 for the commands below (equivalent shell commands also work)

The checked-in `.env.example` contains public, local-only demo credentials—not
deployable secrets. Copy it to the ignored `.env` and change values if desired.

## Start locally

```powershell
Copy-Item .env.example .env
docker compose up -d
docker compose ps

Get-Content .env |
  Where-Object { $_ -and -not $_.StartsWith('#') } |
  ForEach-Object {
    $name, $value = $_.Split('=', 2)
    Set-Item -Path "Env:$name" -Value $value
  }

$env:BATCHDEMO_CONNECTION_STRING = $env:ConnectionStrings__BatchDemo
dotnet tool restore
dotnet tool run dotnet-ef database update `
  --project src/BatchDemo.Infrastructure `
  --startup-project src/BatchDemo.Infrastructure

dotnet run --project src/BatchDemo.Api --launch-profile http
```

In a second shell, load `.env` as above and start the worker:

```powershell
dotnet run --project src/BatchDemo.Worker
```

PostgreSQL is exposed on `127.0.0.1:55432`, MinIO's S3 API on
`127.0.0.1:9000`, and its console on `127.0.0.1:9001`. The one-shot
`minio-init` service creates the `batch-demo` bucket. The API runs at
`http://localhost:5057`; Swagger UI is at `http://localhost:5057/swagger`.
Database migrations are explicit and are never applied by application startup.

Health endpoints:

- `GET /health/live` checks the API process.
- `GET /health/ready` checks PostgreSQL and the configured object-storage bucket.

## Upload examples

With the API running, PowerShell:

```powershell
$response = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5057/api/batches `
  -Form @{
    merchantId = 'merchant_demo'
    file = Get-Item samples/demo-merchant-batch.csv
  }

$response
$terminalStatuses = @('Ready', 'ReadyWithExceptions', 'Rejected', 'ProcessingFailed', 'Duplicate')
do {
  Start-Sleep -Seconds 1
  $result = Invoke-RestMethod -Uri "http://localhost:5057/api/batches/$($response.batchId)"
} while ($result.status -notin $terminalStatuses)
$result

docker compose exec minio-init mc cat "local/$($result.acceptedArtifactKey)"
docker compose exec minio-init mc cat "local/$($result.rejectedArtifactKey)"
docker compose exec minio-init mc cat "local/$($result.summaryArtifactKey)"
```

Or curl:

```bash
curl -i -X POST http://localhost:5057/api/batches \
  -F "merchantId=merchant_demo" \
  -F "file=@samples/demo-merchant-batch.csv;type=text/csv"
```

Successful intake returns `202 Accepted`, a `Location` header, and batch metadata.
The provisional 10,000,000-byte limit remains a later processing rule, so bytes
are preserved before it is evaluated. HTTP transport has a separate 25,000,000-
byte safety ceiling.

## Build and test

```powershell
dotnet restore BatchDemo.sln
dotnet build BatchDemo.sln --no-restore
dotnet test tests/BatchDemo.UnitTests/BatchDemo.UnitTests.csproj --no-build

# Requires the Compose services and the unchanged local demo credentials.
dotnet test tests/BatchDemo.IntegrationTests/BatchDemo.IntegrationTests.csproj --no-build

dotnet format BatchDemo.sln --verify-no-changes --no-restore
```

Stop local infrastructure with `docker compose down`. Add `-v` only when you
intentionally want to delete local PostgreSQL and MinIO volumes.

## Project documents

- [Vision](docs/vision.md)
- [Architecture](docs/architecture.md)
- [Data contract](docs/data-contract.md)
- [Processing flow](docs/processing-flow.md)
- [Intake implementation note](docs/intake-implementation.md)
- [Processing implementation note](docs/processing-implementation.md)
- [Backlog](docs/backlog.md)
- [ADR 0001: Start with a single vertical slice](docs/adr/0001-start-with-single-vertical-slice.md)
- [ADR 0002: Use S3-compatible object storage](docs/adr/0002-use-s3-compatible-object-storage.md)
- [ADR 0003: Use a PostgreSQL-backed work queue](docs/adr/0003-use-postgresql-backed-work-queue.md)
- [ADR 0004: Use processor-neutral canonical payment intent](docs/adr/0004-use-processor-neutral-canonical-payment-intent.md)
- [ADR 0005: Allow partial batch acceptance](docs/adr/0005-allow-partial-batch-acceptance.md)
- [Sample merchant batch](samples/demo-merchant-batch.csv)
