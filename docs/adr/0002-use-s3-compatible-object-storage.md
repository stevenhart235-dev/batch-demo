# ADR 0002: Use S3-compatible object storage

## Status

Accepted

## Context

Original uploads must be retained exactly, and normalized outputs can be larger
than is convenient or appropriate for operational database rows.

## Decision

Use S3-compatible object storage for immutable original uploads and generated
normalized/rejection artifacts. Store object identities and integrity metadata
in PostgreSQL. Use a local S3-compatible service through Docker Compose; do not
operate Ceph in this slice.

## Consequences

Artifact bytes scale independently from workflow state and use a portable API.
The implementation must handle object/database partial failures, immutable key
conventions, checksums, retention, and access controls. S3 compatibility does not
guarantee every provider-specific behavior, so the lab should use a narrow API
surface.
