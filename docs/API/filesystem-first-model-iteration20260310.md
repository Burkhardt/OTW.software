# Filesystem-First API Model (Iteration 2026-03-10)

This iteration adds filesystem-first APIs for Album, Image, and Review while keeping legacy compatibility endpoints alive.

## What Is Derived vs Persisted

- `Album` is derived from filesystem structure:
  - Exists when `ImageServerRootDir/{subscr}/{album}/orig` exists.
  - `totalImages` is computed from distinct image basenames in `orig`.
- `Image` is derived from files in:
  - `ImageServerRootDir/{subscr}/{album}/orig`
  - API returns image identities as basename without extension.
- `Review` is persisted as additional business facts in JsonPit.

## New Endpoints

- `GET /api/albums?subscr=nomsa.net`
- `GET /api/albums/{id}?subscr=nomsa.net`
- `GET /api/images?album=NomsaOB-02&subscr=nomsa.net`
- `POST /api/reviews`
- `GET /api/reviews?album=NomsaOB-02&subscr=nomsa.net&reviewer=Rainer`
- `DELETE /api/reviews?subscr=nomsa.net&reviewer=Rainer&image=nomsa.net/NomsaOB-0222-012`

Legacy compatibility endpoints remain active:

- `GET /api/review/tasks`
- `POST /api/review/decision`

## Review Persistence Strategy

Reviews are stored in JsonPit with one pit per subscriber/album/reviewer.

Current layout:

- `{Reviews:RootDir}/{subscr}/{album}/{reviewer}/`

JsonPit key strategy:

- Key: image basename (for upsert/delete)
- Value: review payload (`Image`, `Vote`, `Reviewer`, `Date`, `Client`, `Device`)

Vote handling:

- Accepted values: `-1` and `+1`
- `0` is rejected and never stored

## Extension-Priority Resolution

The resolver uses extension priority for internal file selection:

1. `.webp`
2. `.png`
3. `.jpg`, `.jpeg`
4. `.tif`, `.tiff`

Public API identity remains basename-only (no extension).

## Temporary vs Final

Current temporary aspects:

- Existing `GET /api/review/tasks` and `POST /api/review/decision` are retained for app compatibility.
- Full image rendering endpoint is not implemented yet; only resolver behavior is prepared in infrastructure.

## Architectural Compromises

- The implementation currently still uses direct `System.IO` calls in infrastructure classes for directory/file enumeration and path handling.
- JsonPit persistence is used for review records, but filesystem access is not yet fully abstracted through final RaiFile-derived adapters.
- Next hardening step: replace remaining direct `System.IO` access with RaiFile/derived abstractions once the required folder-enumeration abstraction is available in the shared libraries.
