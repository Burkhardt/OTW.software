# OTW Documentation

This folder contains implementation-oriented architecture and workflow diagrams for the OTW/AfricaStage prototype.

## UML Diagrams

PlantUML sources are stored in `docs/UML/`:

- `docs/UML/photographer-delivery-state.puml`
  - State machine for `PhotographerDelivery` lifecycle
- `docs/UML/review-run-state.puml`
  - State machine for `ReviewRun` lifecycle with positive-selection flow
- `docs/UML/collaborative-selection-activity.puml`
  - End-to-end collaborative activity flow from ingest through selection and optional curation/pruning

## Notes

- The currently implemented API model is filesystem-first (`Album`, `Image`) with persisted `Review` votes.
- Source share links are ingest references only.
- Canonical runtime image URLs are assigned after import on the AfricaStage/OTW ImageServer.

## API Notes

- `docs/API/filesystem-first-model-iteration20260310.md`
  - Filesystem-first Album/Image model and JsonPit review persistence for the current OTW iteration
- `docs/API/personpit-iteration20260318.md`
  - PersonPit storage layout, JsonPit item model, and the person/contact API added in this iteration
