# PersonPit API Model (Iteration 2026-03-18)

This iteration adds a small JsonPit-backed `PersonPit` feature to OTW for practical diagnostics and contact-data experiments.

## NuGet Packages Used

The feature uses package references, not local project copies, pinned in `OTW.csproj`:

- `JsonPit` `3.5.2`
- `OsLibCore` `3.5.2`
- `RaiUtilsCore` `3.4.0`

## Storage Layout

The configured root path is:

- `PersonPit:RootDir`

Default runtime value:

- `/srv/ServerData/Umshadisi/otw.software/personpit`

Within that root, the person pit is opened as one JsonPit named `person`.

Practical canonical location on disk:

- `{PersonPit:RootDir}/person/person.pit`

JsonPit may also create its normal change-tracking files under the canonical pit folder.

## How The Pit Is Opened

The OTW backend opens the pit with JsonPit's public API:

- `new Pit(pitDirectory.Path, readOnly: false, backup: false, ignoreCase: true)` for writes
- `new Pit(pitDirectory.Path, readOnly: true, unflagged: true, ignoreCase: true)` for reads

The `pitDirectory.Path` value is built with `OsLib.RaiPath`, so path handling remains directory-aware and uses the packaged abstraction instead of ad hoc string concatenation alone.

## Person Representation

Each person is one JsonPit `PitItem` in the `person` pit.

- JsonPit key: `Name`
- External lookup key: `Name`
- Current fixed attributes supported on create:
  - `Email`
  - `Instagram`
  - `Facebook`
  - `Phone`
  - `ComPref`

Example conceptual `PitItem`:

```json
{
  "Name": "Max",
  "Email": "max@example.com",
  "Instagram": "@maxmusic",
  "Facebook": "max.artist",
  "Phone": "+16195551212",
  "ComPref": ["WhatsApp", "Email"]
}
```

JsonPit metadata such as `Modified`, `Deleted`, and `Note` remains internal and is not returned from the PersonPit API.

## Create Behavior

`POST /api/persons` is intentionally create-only.

- if `Name` does not exist: a new `PitItem` is added and saved
- if `Name` already exists: OTW returns `409 Conflict`

This keeps create semantics simple and leaves follow-up changes to the named-attribute endpoint.

## Named Attribute Updates

`POST /api/persons/{name}/attributes` works like this:

1. load the existing person by `Name`
2. build a single-property JSON object for the requested attribute
3. call JsonPit `PitItem.SetProperty(...)`
4. write the updated `PitItem` back through `pit.PitItem = person`
5. persist with `pit.Save(force: true)`

The endpoint accepts JSON scalars, arrays, and objects. `ComPref` is validated specially and must remain an array of allowed values.

## Attribute Existence Query

`GET /api/persons?hasAttribute=Instagram` checks current undeleted `PitItem` objects in the `person` pit and returns those where:

- the named property exists
- the property name matches case-insensitively
- the value is not `null` and not `undefined`

The query is generic, so the same mechanism works for `Instagram`, `Facebook`, `Address`, `Website`, or any later attribute added through the attribute endpoint.

## RaiUtils Usage

The implementation uses RaiUtils helpers where they directly help this feature:

- `RaiUtils.Email` validates `Email` syntax on create
- `RaiUtils.JsonConversionExtensions` converts nested `JObject` and `JArray` values into plain response objects for API serialization

## OsLib Usage

The implementation uses OsLib abstractions for path and directory handling:

- `RaiPath` for the configured root and pit directory composition
- `RaiFile` for canonical pit file awareness and directory materialization

This keeps the feature aligned with the practical file-abstraction intent without redesigning the rest of OTW.