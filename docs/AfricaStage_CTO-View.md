# AfricaStage CTO View

## Executive Summary

The project has moved from abstract planning into a working early prototype phase.

A first end-to-end vertical slice now exists:

- `otw.software` is running on Ubuntu behind **Caddy** with **HTTPS**
- a minimal **OpenAPI/Swagger** backend is working
- a first **SwiftUI iPhone/iPad app** (`ImageReview`) is working against that backend
- the app can:
  - fetch review tasks
  - display remote images
  - post decisions back to the server
  - remove handled tasks from the visible list immediately

At the moment, `otw.software` is acting as a **temporary stand-in for AfricaStage** so the workflow can be tested without waiting for the full AfricaStage service hub to be built.

---

## Architectural Direction

### Confirmed strategic direction

- **AfricaStage will be the hub**
- **AfricaStage should become the canonical host for images** used by Nomsa.net
- image ingestion may start from:
  - direct upload
  - Google Drive / Google Photos shared links
- the iOS/iPadOS app is meant to support:
  - review
  - selection
  - later face labeling
- Nomsa.net will later consume images and metadata from the AfricaStage ImageServer instead of local disk

### Technology direction

- Backend platform: **.NET / C#**
- Reverse proxy: **Caddy**
- Persistence: **JsonPit only**
- Querying: **LINQ**, not SQL
- File handling: **RaiFile / JsonFile / derived RaiFile classes only**
- No direct `System.IO` in final architecture
- OTW prototype currently serves as an experimental implementation host

---

## What is Working Right Now

### 1. Reverse proxy / HTTPS

`otw.software` is working behind Caddy with a free SSL certificate.

Current remembered Kestrel port plan:

- `5012` -> `OTW.software`
- `5011` -> `OWiS.software`
- `5001` -> `AfricaStage.com`
- `5002` -> `AIA.software`
- `Nomsa.net` -> later

### 2. OTW mock backend

A minimal backend is working with:

- `GET /api/review/tasks`
- `POST /api/review/decision`

OpenAPI/Swagger is working and now correctly reflects **https** after forwarded-header handling was fixed.

### 3. SwiftUI app: `ImageReview`

A first iOS/iPadOS SwiftUI client is working.

Current capabilities:

- fetches tasks from `https://otw.software/api/review/tasks`
- shows the task list
- shows detail view with remote image
- posts Keep / Reject / Skip decisions
- automatically returns to list after decision
- removes the handled task locally from the list

### 4. Mock task source via JSON file

A `tasks.json` based task source has been introduced conceptually and partly wired into the backend via configuration.

Configured path used by OTW:

`/srv/ServerData/Umshadisi/otw.software/review/tasks.json`

Important operational note:
there was evidence that files under the synchronized Umshadisi area may disappear or be overwritten by sync behavior. This needs to be understood and stabilized before it becomes trusted operational storage for this workflow.

---

## Important Architectural Decisions Already Made

### Persistence and file architecture

The following are hard constraints:

- all persistence uses **JsonPit**
- all querying uses **LINQ**
- no SQL
- all file handling uses **RaiFile** and derived classes
- no direct `System.IO.File`, `Directory`, `Path`, etc. in final code

### Web architecture

Recommended direction:

- **Minimal APIs** for service/API endpoints
- **Razor Pages** for HTML pages and admin/tooling pages

Reason:
inline HTML in `Program.cs` was judged too error-prone and too weak in editor assistance compared to Razor.

### Image server direction

The long-term direction is an **AfricaStage ImageServer** that:

- stores canonical originals and derivatives
- supports lazy rendering by template
- supports in-memory caching for hot variants
- serves stable URLs to consumers such as Nomsa.net
- stores AI-derived metadata alongside image assets
- can support recommendation and agentic workflows

---

## Current Conceptual Model Evolution

The project started with a very small "review task" concept but has already evolved significantly.

### Earlier model
- static `tasks.json`
- review task states like `Pending`, `Accepted`, `Rejected`, etc.

### Current better model
The currently implemented backend model is centered around:

- **Album** (filesystem-derived)
- **Image** (filesystem-derived)
- **ReviewVote** (JsonPit-persisted business fact)

### Key conceptual shift
The model has shifted away from task-status metadata and toward a non-redundant filesystem-first design.

- Albums and images are discovered from ImageServer folder structure
- Image identity is basename-only (without extension)
- Only additional business facts (review votes) are persisted
- Redundant metadata that can be derived from folders/files is intentionally not stored

---

## Current Open Questions

### 1. Google Photos / Google Drive ingestion

A photographer currently shares albums like:

`https://photos.app.goo.gl/YSsDVYRVhr31ndY8A`

The intended product behavior is:

- discover/list albums from subscriber folders
- discover/list album images from `orig` folders
- persist reviewer votes as additional business facts
- serve canonical URLs from AfricaStage/OTW after import

This still needs proper implementation.

### 2. Review persistence hardening

The backend now persists review votes via JsonPit, but hardening is still needed.

Current:
- `POST /api/reviews` upserts one vote (`-1` / `+1`)
- `GET /api/reviews` reads reviewer+album votes
- `DELETE /api/reviews` removes one vote

Next:
- add integration tests for upsert/read/delete behavior
- harden path/sanitization and collision handling
- keep contracts stable while internals mature

### 3. Synced storage semantics

The behavior of the synchronized Umshadisi directory needs clarification:
- whether it is one-way mirrored
- whether local additions are preserved
- whether it is safe for workflow-state files

### 4. Final hosting model

The strategic decision is already:
- **AfricaStage hub now**

But the OTW prototype is still the active implementation sandbox.
A staged migration or re-home into AfricaStage proper remains to be executed.

---

## Suggested Immediate Next Steps

### Near-term

1. **Review Mzansi Agent UML output**
   - especially State + Activity Diagrams for the updated collaborative workflow

2. **Check in the current OTW and ImageReview work to GitHub**

3. **Refactor OTW landing page to Razor Pages**
   - keep Minimal APIs unchanged
   - remove fragile inline HTML

4. **Replace temporary direct file I/O helper with RaiFile / JsonFile compliant implementation**
   - this is required for architectural cleanliness

5. **Clarify synchronized storage behavior**
   - understand why created JSON disappeared
   - decide how to safely use replicated storage in the prototype

### After that

6. **Introduce richer workflow objects only when needed**
  - start from implemented `Album` / `Image` / `ReviewVote`
  - add additional entities only when they provide non-derivable value

7. **Move from mock tasks toward discovered/imported image assets**

8. **Add configurable server selection in the Swift app**
   - OTW now
   - AfricaStage later

9. **Plan ImageServer implementation more concretely**
   - originals
   - variants
   - templates
   - lazy generation
   - cache
   - locking / single-flight rendering

---

## Agent Landscape

Multiple "agents" are currently contributing, each with a distinct role.

### 1. Eliza (this assistant)
**Role:** architecture, systems thinking, workflow design, review, coordination

**Current status from my perspective:**
- strong on overall direction
- helped define:
  - AfricaStage hub strategy
  - OTW as prototype
  - HTTPS + Caddy path
  - API-first mock workflow
  - Swift client scope
  - collaborative selection model
- also acting as architecture guardrail for:
  - JsonPit-only persistence
  - LINQ not SQL
  - RaiFile not System.IO

**Strengths so far:**
- keeping the big picture coherent
- preventing architectural drift
- translating evolving product ideas into implementable structure

**Risk / limitation:**
- not the one directly editing the codebase in the IDE
- depends on review loops with the other agents and the user

---

### 2. Mzansi Agent
**Role:** backend implementation / code changes / UML generation / prototype implementation assistant

**Current status from my perspective:**
- useful implementation partner
- has already made productive changes:
  - forwarded headers fix
  - appsettings update
  - backend changes
- sometimes drifts toward expedient implementation choices

**Observed concerns:**
- used direct `System.IO` in `Program.cs` helper despite architectural constraint
- allowed fragile inline HTML to remain until challenged
- needs explicit reminders to preserve existing API contracts

**Overall assessment:**
- valuable and productive
- should be treated as a capable implementation agent that still needs architectural supervision

---

### 3. GPT-5.3 Codex in Xcode ("cousin Codex")
**Role:** SwiftUI client scaffolding and local code generation inside Xcode

**Current status from my perspective:**
- performed very well for first-pass app scaffolding
- helped build the first working `ImageReview` app quickly
- adapted follow-up UX refinements after prompting

**Observed concerns:**
- some fixes were only partially right on the first try
- needed follow-up prompts when the local task removal behavior did not actually work
- typical for scaffolding/codegen: useful but should not be trusted blindly

**Overall assessment:**
- strong productivity booster for SwiftUI
- especially valuable because Swift is currently less comfortable territory than .NET
- should continue to be used with tight prompts and quick review/testing

---

### 4. The user (Rainer) as human lead / product shaper
**Role:** CTO, product owner, architect, domain expert, operations lead, workflow designer

**Current status from my perspective:**
- driving the most important conceptual improvements
- repeatedly corrected the model in highly valuable ways:
  - AfricaStage hub now
  - image server as canonical host
  - positive-selection workflow instead of rejection-centric workflow
  - insistence on JsonPit / RaiFile / opinionated architecture
  - production realism from earlier ImageServer experience

**Overall assessment:**
- the project direction is getting better specifically because of Rainer's interventions
- the strongest product and architecture refinements are currently emerging from the discussion loop between Rainer and the assisting agents

---

## Overall Project Health

### Positive
- real progress was made
- first vertical slice is working
- infrastructure path is proven
- app + backend integration is real, not hypothetical
- architecture is becoming sharper, not fuzzier

### Caution areas
- prototype code still contains architectural shortcuts
- sync/replication behavior is not yet operationally well understood
- data model is evolving quickly and should be formalized soon
- OTW is still a stand-in, so migration into true AfricaStage structure must eventually happen

### Bottom line
The project is in a very good early prototype state:
**implementation has started, critical technical unknowns are being reduced, and the conceptual model is improving quickly.**

That is exactly where a promising system should be at this stage.

---

## Mzansi Agent Implementation Perspective (As of 2026-03-10)

### What is now concretely implemented in OTW

- OTW backend is now a hybrid ASP.NET Core app:
  - Minimal APIs for `/api/review/*`
  - Razor Pages for `/` landing page
- The large inline HTML payload has been removed from `Program.cs` and moved to `Pages/Index.cshtml`.
- The markup typo in the old inline page (`</cdarticle>`) has been corrected in the Razor version.
- Static files continue to serve from `wwwroot` (CSS and image assets).
- OpenAPI and Swagger are active and reachable.
- Forwarded headers middleware is active early in the pipeline and configured to honor:
  - `X-Forwarded-For`
  - `X-Forwarded-Proto`
  - `X-Forwarded-Host`
- This fixed the proxy-awareness issue so Swagger/OpenAPI can resolve host/scheme correctly behind Caddy.

### Current API behavior status

- New filesystem-first endpoints:
  - `GET /api/albums`
  - `GET /api/albums/{id}`
  - `GET /api/images`
  - `POST /api/reviews`
  - `GET /api/reviews`
  - `DELETE /api/reviews`
- Legacy compatibility endpoints remain active:
  - `GET /api/review/tasks`
  - `POST /api/review/decision`
- `/api/selections` endpoints were removed from OTW implementation.

- `GET /api/review/tasks`
  - contract unchanged
  - reads JSON from configured `ReviewTasks:FilePath`
  - default path remains `/srv/ServerData/Umshadisi/otw.software/review/tasks.json`
  - returns clear Problem responses when source file is missing/invalid/unreadable
- `POST /api/review/decision`
  - contract unchanged
  - still mock-acknowledges with `{ ok: true }`
  - no persistence yet (by design in current prototype stage)

### Documentation and modeling status

- PlantUML workflow diagrams were added and are now checked into:
  - `OTW/docs/UML/photographer-delivery-state.puml`
  - `OTW/docs/UML/review-run-state.puml`
  - `OTW/docs/UML/collaborative-selection-activity.puml`
- Documentation index is in `OTW/docs/README.md`.
- Diagrams reflect the positive-selection model with optional later curation/pruning.

### Known implementation gap vs architecture policy

- The current `JsonFile` helper implementation still internally uses direct `System.IO` (`File.Exists`, `File.ReadAllText`).
- This is the primary mismatch against the agreed architecture rule (`RaiFile`/JsonPit style file access only, no direct `System.IO` in final implementation).

### Recommended technical next step (implementation-first)

1. Replace current temporary `JsonFile.TryRead<T>()` internals with true RaiFile-based storage access.
2. Keep persisted model focused on `ReviewVote` until additional state is proven necessary.
3. Keep current API contracts stable while evolving backend internals and persistence.
4. Add one integration test for forwarded-header + Swagger/OpenAPI scheme correctness behind proxy headers.
5. Add one integration test that validates Problem responses for missing/invalid tasks JSON.

### Confidence statement

- The prototype is operational and useful today.
- Architecture guardrails are clear.
- The highest-value improvement now is replacing temporary file access internals while preserving API stability.
