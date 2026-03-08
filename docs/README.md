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

- The workflow is positive-selection based (`SelectionMark`) rather than rejection-driven.
- Source share links are ingest references only.
- Canonical runtime image URLs are assigned after import on the AfricaStage/OTW ImageServer.
