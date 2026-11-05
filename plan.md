# rldump — Implementation Plan

A C# / .NET 10 CLI tool that queries the Rocket League "Prime" game API (hosted at
`https://prime.rocketplanet.gg`) and prints the results as JSON. The JSON is intended to be
loaded into a web page for display (a future, separate project). v1 supports the current
tournament schedule for a region, plus a `regions` helper command. The tool ships as a
self-contained **Native AOT** single-file executable. The full toolchain is managed by
**mise** (same convention as the sibling `gravwar` project).

---

## 1. Locked-in decisions

| Topic | Decision |
| --- | --- |
| Language / runtime | C# 14, .NET 10 (`net10.0`), SDK **10.0.403** installed via mise |
| CLI parsing | **System.CommandLine 2.0.14** (current stable NuGet package; not in the .NET 10 BCL) |
| Executable | Native AOT: `<PublishAot>true</PublishAot>` + `<InvariantGlobalization>true</InvariantGlobalization>` → `dist/rldump` |
| AOT C compiler | **clang 23.1.4** via mise (system only has gcc; keeps the toolchain hermetic) |
| JSON | `System.Text.Json` with **source-generated** `JsonSerializerContext` only (no reflection — required for AOT) |
| Output format | **Tidy by default**: camelCase keys, ISO 8601 UTC timestamps, IDs as strings, `b`-prefix dropped from booleans, game-mode indices replaced with names. `--raw` passes the API response through unmodified |
| Forward compatibility | Only the **transformed** fields are explicitly modeled in response types; all other wire fields are captured with `[JsonExtensionData]` (`Dictionary<string, JsonElement>`) and passed through to the tidy output (keys camelCased in the mapping step) — new API fields never get silently dropped |
| Game modes | Index→name mapping from `gamemodes.json` (embedded resource). Unknown indices pass through as raw numbers. Tidy mode only |
| Token / config | **Config file only** (`rldump.json`; tracked example: `rldump.json.example`). Search order: `./rldump.json` → `$XDG_CONFIG_HOME/rldump.json` (default `~/.config/rldump.json`). Per-field merge, cwd wins. `--config <path>` flag overrides search entirely |
| v1 scope | `rldump schedule --region USW` + `rldump regions` |
| Tests | **xunit v3, 4.x line**: `xunit.v3.mtp-v2` **4.0.2** with Microsoft.Testing.Platform; fake `HttpMessageHandler` + fixtures, no network in tests |
| CI | Two GitHub Actions workflows (gravwar's split): `pull_request.yml` runs the build/test/AOT-publish gate on PRs to `main`; `release.yml` runs the same gate on pushes to `main`, then packages the `rldump-linux-x64-<version>.tar.zstd` artifact (binary + `rldump.json.example`, `archive: false`) and creates a GitHub release (tag `v<version>`, tarball attached, gravwar-style commit-list notes). Version = major.minor of the csproj `<Version>` + `GITHUB_RUN_NUMBER` as the patch (gravwar-style); publish stamps the binary so `rldump --version` = `1.0.<run>`. Actions: `actions/checkout@v7` + `jdx/mise-action@v4` + `actions/upload-artifact@v7` + `softprops/action-gh-release@v3` |

### Rationale notes

- **IDs as strings**: the API contract explicitly warns that ID/timestamp fields are 64-bit
  QWord values that "may exceed JavaScript safe-integer precision". Since a web page will
  consume this JSON, serializing IDs as strings avoids silent precision loss in JS.
- **ISO 8601 timestamps**: the API returns unix epoch seconds (`1793826000`); the web page
  benefits from `"2026-11-05T01:00:00Z"`-style strings.
- **Config file only for the token** (per user): tokens are pre-issued from the
  rocketplanet.gg account portal (`/access`) and expire; a local config file keeps them out
  of shell history and process listings.
- **xunit v3 4.x** (per user): the `xunit.v3.mtp-v2` package is the default-template core
  package for the 4.0.x line; test projects are standalone executables run through
  Microsoft.Testing.Platform (`dotnet test` uses MTP when a root `global.json` declares it).

---

## 2. Verified API facts (from live docs data endpoints)

The docs site (`prime.rocketplanet.gg/docs`) is a SPA that loads machine-readable data:

- Route index: `GET https://prime.rocketplanet.gg/docs/index.json` — 234 endpoints, all with
  HTTP method **`QUERY`**.
- Per-route contract: `GET https://prime.rocketplanet.gg/docs/contract?service=<Service/Route>`
  — request/response TypeScript types + diagnostics. **Use these as the canonical reference
  when adding endpoints.**

### 2.1 Transport

- Base URL: `https://prime.rocketplanet.gg`
- Every endpoint uses the custom HTTP verb **`QUERY`** (see `curl -X QUERY`). .NET 10
  ships it as a built-in static: use `HttpMethod.Query` (introduced in .NET 10 — do not
  construct `new HttpMethod("QUERY")`).
- Headers: `Authorization: Bearer <token>`, `Content-Type: application/json`.
- Request body: JSON. Response body: JSON.

### 2.2 Response envelope (all endpoints)

```
{ "Error": { "Message": string, "Type": string } }        // failure
{ "Result": <endpoint-specific payload> }                 // success
```

`Error.Type` for tournament routes includes a `TourErrors_TA` union
(e.g. `ScheduleRegionIsInvalid`, `TournamentNotFound`, …) plus transport errors
(`Disconnected`, `NotLoggedIn`, `TimedOut`).

### 2.3 `Tournaments/Search/GetSchedule`

- Request body: `{ "Region": "USW" }` — a `RegionId` string.
  The reflected schema also lists `PlayerID`, but live requests succeed without it
  (verified by a live curl request). If the API ever starts requiring it, add a
  `--player-id` option; do not send it by default.
- Known `RegionId` values (open set; the API validates and returns
  `ScheduleRegionIsInvalid` for bad ones):
  `ASC ASM EU IND JPN ME OCE SAF SAM USC USE USW`
- Result payload: `{ "Schedules": ScheduledTournament[] }` where

```
ScheduledTournament = {
  Time: uint64 (unix seconds),
  ScheduleID: uint64,
  bUpdateSkill: bool,
  Tournaments: TourSettings_TA[]
}
```

- `TourSettings_TA` (union of the reflected type and the observed live response — the
  reflected type is incomplete; e.g. live responses include `IsSchedulingTournament`):

| Wire field (PascalCase) | Type | Notes |
| --- | --- | --- |
| `ID` | uint64 | tournament ID |
| `Title` | string | e.g. `"3v3 Dropshot"` |
| `Description` | string? | present in reflected type, not in sample |
| `CreatorName` | string | e.g. `"Psyonix"` |
| `CreatorPlayerID` | string | e.g. `"Steam\|0\|0"` |
| `StartTime` | uint64 | unix seconds |
| `GenerateBracketTime` | uint64? | null in samples |
| `MatchExpireTimestamp` | uint64? | reflected type only |
| `MaxBracketSize` | int | |
| `RankMin` / `RankMax` | int | |
| `Region` | string | region code |
| `RegionName` | string? | reflected type only |
| `Platforms` | string[] | e.g. `["Steam","PS4","XboxOne","Switch","Epic"]` |
| `GameTags` | string | `""` in samples |
| `GameMode` | int | mode index (see §6) |
| `GameModes` | int[] | mode indices; non-empty for composite modes (Pentathlon) |
| `TeamSize` | int | 2 or 3 |
| `MinPlayersPerTeam` | int? | reflected type only |
| `MapSetName` | string? | |
| `DisabledMaps` | string[] | map asset names, `[]` for some modes |
| `SeriesLength` / `FinalSeriesLength` | int | |
| `SeriesRoundLengths` | int[] | e.g. `[3,3,1]` |
| `SeedingType` | int | 0=Skill, 1=Random (`ETourSeedType`) |
| `TieBreaker` | int | 0=FirstScore, 1=Random (`EMatchTieBreaker`) |
| `bPublic` | bool | |
| `bIsMultiGameMode` | bool? | reflected type only |
| `TeamsRegistered` | int | |
| `ScheduleID` | uint64 | |
| `IsSchedulingTournament` | bool? | live response only |

**Modeling note**: the table above is the *reference* shape. The C# models explicitly
declare **only the transformed fields** (see §9.1); everything else is captured via
`[JsonExtensionData]` and passed through. New wire fields therefore appear in tidy
output automatically.

### 2.4 `Config/GetScheduleRegions`

- Request body: `{}` (empty object).
- Contract marks the result as **opaque** (`"Result": {}`) — no reflected shape.
- Therefore `rldump regions` prints the `Result` payload **verbatim** (pretty-printed);
  normalization is impossible for an unknown shape.

---

## 3. Repository layout

```
rldump/
├── plan.md
├── mise.toml
├── global.json                     # { "test": { "runner": "Microsoft.Testing.Platform" } }
├── rldump.slnx
├── AGENTS.md
├── README.md
├── LICENSE                         # MIT (repo only — not part of the release tarball)
├── rldump.json.example           # tracked example config (copy → rldump.json, fill token)
├── .gitignore
├── .github/workflows/pull_request.yml  # PRs to main: build/test/publish gate
├── .github/workflows/release.yml       # pushes to main: gate + tarball + GitHub release
├── src/rldump/
│   ├── rldump.csproj
│   ├── Program.cs                  # entry point; builds root command, maps exit codes
│   ├── Data/
│   │   └── gamemodes.json          # EMBEDDED resource (copy of the extracted mapping)
│   ├── Cli/
│   │   ├── RootCommand.cs          # global options, description, version
│   │   ├── ScheduleCommand.cs
│   │   └── RegionsCommand.cs
│   ├── Api/
│   │   ├── RocketPlanetClient.cs   # QUERY verb, bearer auth, timeout, envelope handling
│   │   ├── ApiError.cs             # { Message, Type }
│   │   ├── GetSchedule.cs          # request + response models
│   │   └── GetScheduleRegions.cs
│   ├── Output/
│   │   ├── ScheduleOutput.cs       # tidy output models
│   │   ├── GameModeName.cs         # string|number output type + converter
│   │   ├── GameModeMap.cs          # loads embedded mapping → int→string dictionary
│   │   └── RldumpJson.cs           # JsonSerializerContext(s) + shared options
│   └── Config/
│       ├── RldumpConfig.cs         # { api: { baseUrl, token } }
│       └── ConfigLoader.cs         # search order, per-field merge, errors
└── tests/rldump.Tests/
    ├── rldump.Tests.csproj
    ├── Fakes/FakeHttpHandler.cs    # canned JSON responses keyed by URL
    ├── Fixtures/
    │   ├── getschedule-success.json    # verbatim copy of a real API response
    │   └── getschedule-error.json      # synthetic Error envelope
    ├── ConfigLoaderTests.cs
    ├── RocketPlanetClientTests.cs
    ├── ScheduleCommandTests.cs
    ├── ScheduleOutputTests.cs        # tidy transformation incl. game-mode names
    ├── GameModeMapTests.cs
    └── RegionsCommandTests.cs
```

---

## 4. Toolchain files

### 4.1 `mise.toml`

Pins `dotnet 10.0.403` and `clang 23.1.4` (the AOT compiler; the system only has
gcc — keeps the toolchain hermetic). Tasks:

- `setup` — `dotnet restore` (sourced on `rldump.slnx`, so it re-runs when the
  solution changes).
- `build` — `dotnet build -c Release --no-restore`.
- `test` — `dotnet test --no-build -c Release` (MTP runner, per root `global.json`).
- `publish` — `dotnet publish src/rldump/rldump.csproj -c Release -r linux-x64 -o
  dist`; when `RLDUMP_VERSION` is set (CI sets it to `<major.minor>.<run-number>`)
  the binary is additionally stamped with it via `-p:Version=` — locally it keeps
  the csproj `<Version>`.

First-time setup: `mise trust && mise install && mise run setup`.
(First `mise install` pulls the .NET SDK + clang — the one slow step.)

### 4.2 `global.json` (repo root)

Declares the Microsoft.Testing.Platform runner so `dotnet test` drives the xunit v3
executable test project (per xunit docs, *Microsoft Testing Platform in v3*).

### 4.3 `src/rldump/rldump.csproj`

A plain `Microsoft.NET.Sdk` `Exe` targeting `net10.0` (assembly `rldump`, namespace
`Rldump`, nullable + implicit usings). The properties that matter:
`<PublishAot>true</PublishAot>` + `<InvariantGlobalization>true</InvariantGlobalization>`
(the AOT single-file build, §1); `<Version>1.0.0</Version>` (the CI version base);
`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`;
`<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>`;
the single package reference `System.CommandLine` 2.0.14; `Data/gamemodes.json` as an
`EmbeddedResource`; and `InternalsVisibleTo rldump.Tests`.

Notes:

- `PublishAot` in the csproj also enables trim/dynamic-analysis on regular builds; that's
  acceptable for a small CLI and surfaces AOT problems early.
- `InvariantGlobalization` avoids an ICU dependency; we format no culture-specific data.
- `TreatWarningsAsErrors` catches trim warnings at build time.
- No `LangVersion` needed — .NET 10 defaults to C# 14.
- `<Version>` is the version base (`1.0.0`): CI keeps major.minor and replaces the
  patch with the GitHub run number via `dotnet publish -p:Version=1.0.<run>` (the
  `mise run publish` task does this when `RLDUMP_VERSION` is set). Locally, `--version`
  prints the csproj value verbatim.
- `IncludeSourceRevisionInInformationalVersion=false`: inside a git repo the SDK
  imports `Microsoft.Build.Tasks.Git`, which sets `SourceRevisionId` (from
  `GITHUB_SHA` on CI), and the SDK's `AddSourceRevisionToInformationalVersion`
  target then appends `+<SourceRevisionId>` to the informational version — so on
  CI `--version` would print e.g. `1.0.57+ef46…`, breaking the CI smoke's
  exact-match assertion. Opting out keeps `--version` exactly the (stamped)
  `$(Version)`. Note `AssemblyInformationalVersion` does *not* suppress this —
  the attribute is generated from `InformationalVersion`, which the SDK never
  reads from `AssemblyInformationalVersion`.
- `InternalsVisibleTo rldump.Tests` lets the test project drive internal types directly
  (the CLI is an `Exe` with no public API — see §9 for the visibility rule).

### 4.4 `tests/rldump.Tests/rldump.Tests.csproj`

xunit v3 4.x project shape (per the official 4.0.x template / getting-started docs):
an `Exe` on `net10.0` that project-references `src/rldump`, with
`<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>` and the
single package `xunit.v3.mtp-v2` 4.0.2.

- Test projects are standalone executables (MTP-based); `dotnet test` runs them via the
  runner declared in the root `global.json`.
- The test project must **not** be AOT-published; only `src/rldump` is published.
  (Guard: `PublishAot` is set only in the main csproj.)
- The test project deliberately carries **no `<Version>`**. SCL's default `--version`
  prints the *entry* assembly's informational version, which in the test host is the
  test assembly (SDK default `1.0.0`), not rldump's — so
  `CliWiringTests.Version_Exit0_PrintsVersion` asserts the shape of the output
  (exit 0, a version-looking line, with an optional `+<build metadata>` suffix —
  on CI the SDK appends `+<GITHUB_SHA>` to the test assembly, which is not
  pinned the way the main csproj is) rather than its value, and the real
  binary's version is covered by the CI smoke step instead.
- An `xunit.runner.json` is optional; only add it if we need non-default runner config.

### 4.5 `.gitignore`

Ignores build output (`src/*/bin|obj`, `tests/*/bin|obj`, `dist/`), the local
`rldump.json` (may contain API tokens — the `.example` is tracked), IDE/editor
files, and `*.log`.

(`rldump.json` is gitignored at repo root only — the embedded
`src/rldump/Data/gamemodes.json` and the tracked `rldump.json.example`
stay tracked.)

---

## 5. CLI design

### 5.1 Commands and options

```
rldump [command] [options]

Commands:
  schedule    Get the current tournament schedule for a region
  regions     List the valid schedule regions

Global options:
  --config <PATH>    Use a specific config file (skips the default search)
  --raw              Print the API's JSON response unmodified (default: tidy output)
  --compact          Print compact JSON (default: indented)
  -o, --out <FILE>   Write JSON to FILE instead of stdout
  --timeout <SECS>   HTTP timeout in seconds (default 30)
  -h, --help         Help
  --version          Version

schedule:
  rldump schedule --region USW [--raw] [--compact] [-o FILE] [--timeout SECS] [--config PATH]
  --region <CODE>    Schedule region (required), e.g. USW

regions:
  rldump regions [--raw] [--compact] [-o FILE] [--timeout SECS] [--config PATH]
```

Behavior:

- JSON always goes to **stdout** (or `-o FILE`); human-readable diagnostics always to
  **stderr**. Never mix.
- `--region` is passed through as-is; the API validates and answers with
  `ScheduleRegionIsInvalid` for unknown codes.
- `regions` prints the `Result` payload verbatim (opaque shape, §2.4); `--raw` there is
  effectively a no-op distinction (envelope vs payload) — implement as: tidy = `Result`
  payload, raw = full envelope.

### 5.2 Exit codes

| Code | Meaning |
| --- | --- |
| 0 | success |
| 1 | API error (`Error` envelope, or non-2xx HTTP response) |
| 2 | usage error (System.CommandLine default for parse errors) |
| 3 | configuration error (no config found, missing token, malformed JSON, unreadable file) |
| 4 | network/transport error (DNS, connect, timeout) |

`-o FILE` failure (unwritable path) also exits 3 with a stderr message.

### 5.3 Error message shapes (stderr)

```
rldump: no API token: no rldump.json in '.' and no ~/.config/rldump.json
rldump: API error (ScheduleRegionIsInvalid): The schedule region 'XYZ' is not valid.
rldump: request failed: The request timed out after 30s.
rldump: HTTP 503 from /Tournaments/Search/GetSchedule: Service Unavailable
```

The bearer token must never appear in any error output or log (defensively redact the
config's token value from any message built from response text).

---

## 6. Game-mode name mapping

Source: `gamemodes.json` (extracted from the game binary), **embedded as a
resource** at `src/rldump/Data/gamemodes.json`.

- Only two fields are used: `GameMode Index` (int) and `GameMode Name` (string). The other
  fields (`GameMode Map Set`, `GameMode Disallowed Mutators`, `GameMode GameInfo Path`) are
  kept in the file for provenance but ignored at runtime.
- The file currently covers indices 0–3, 5–33 (33 entries; **index 4 is missing**).
- `GameModeMap` (in `Output/`) loads the embedded resource at startup, parses it with a
  source-generated `JsonTypeInfo`, and builds an `IReadOnlyDictionary<int, string>`.
  Loading happens once, before the first command invocation; a malformed embedded resource
  is a programmer error → crash with a clear message (it cannot happen for a shipped
  binary, and failing loud is correct).

### Tidy-mode transformation (non-`--raw` only)

- `GameMode` (int) and every `GameModes` entry: known index → **name string**
  (`"gameMode": "Dropshot"`).
- Unknown index (4, or any future index) → **kept as the raw number**
  (`"gameMode": 4`). No invented names; no data loss. The field is therefore
  `string | number` by design — document this in the README for the web page consumer.
- `--raw` mode is never transformed.

Worked example from the fixture (schedule 60104, "2v2 Pentathlon"):

```
GameMode: 27, GameModes: [3, 12, 20, 1, 6]
→  "gameMode": "Pentathlon Tournament",
   "gameModes": ["Rumble", "Dropshot Rumble", "Super Cube", "Hoops", "Heatseeker"]
```

Updating the mapping when the game ships a new build: replace
`src/rldump/Data/gamemodes.json` and rebuild. Note this in AGENTS.md.

---

## 7. Config resolution

### 7.1 File shape

The config file is named `rldump.json`; a tracked, secret-free example ships as
`rldump.json.example` (copy it to `rldump.json` — or anywhere, and point
`--config` at it — and fill in the token).

Shape: one top-level `api` object holding `baseUrl` and `token`.

- `api.token` — **required** (non-empty) for any command that talks to the API.
- `api.baseUrl` — optional; default `https://prime.rocketplanet.gg`. Trailing slashes are
  trimmed.
- Unknown keys: ignored (forward compatible). Wrong-typed keys: config error (exit 3).

### 7.2 Search order and merge

1. `--config <PATH>` if given: exactly that file, must exist and be valid (exit 3 otherwise).
2. Else: `./rldump.json` (current working directory), then
   `$XDG_CONFIG_HOME/rldump.json` (fallback `~/.config/rldump.json` when
   `XDG_CONFIG_HOME` is unset/empty).
3. **Per-field merge**: when both files exist, cwd wins field-by-field
   (`api.baseUrl`, `api.token`). A file may contain just one field.
4. Missing/empty token after merge → exit 3 with a message that names the files searched.
5. Files are read as UTF-8; JSON parse errors → exit 3 with the file path and the
   System.Text.Json error position.

Tests control resolution by setting `Environment.CurrentDirectory` and
`XDG_CONFIG_HOME`/`HOME` to temp directories (and via `--config`).

---

## 8. API client

`RocketPlanetClient` — a small, testable class (no statics):

- Ctor: `(string baseUrl, string token, TimeSpan timeout, HttpMessageHandler? handler = null)`.
  The optional handler injection is the test seam (fake handler in tests).
- `Task<T> QueryAsync<T>(string path, object request, JsonSerializerContext context, CancellationToken ct)`:
  - Request using the built-in `HttpMethod.Query` (static property, .NET 10+) to `{baseUrl}{path}`.
  - Headers: `Authorization: Bearer {token}`, `Content-Type: application/json`.
  - Body: request serialized via the source-generated context (camelCase is irrelevant on
    the wire — the API uses PascalCase, so request serialization uses the default
    `null` naming policy; C# property names match the wire names).
  - Non-2xx → throw `ApiHttpException` (status + trimmed body, token redacted).
  - 2xx body deserialized as the endpoint's envelope type
    (`Envelope<TResult>` with `Error` / `Result` properties).
  - `Error != null` → throw `ApiErrorException { Type, Message }`.
  - Returns `Result`.
- `HttpClient` created per client instance; `Timeout` from config.
- Network/timeout failures surface as `HttpRequestException`/`TaskCanceledException` →
  exit 4 at the CLI layer.

### Envelope model

`Envelope<TResult>` pairs an `ApiError? Error` with a `TResult? Result` (init-only,
`internal sealed`); `ApiError` carries `Message` and `Type` strings.

---

## 9. Models and serialization

**Visibility rule**: `rldump` is an `Exe` with no public API surface, so *every*
top-level type is `internal` (a top-level type can be `public` or `internal`, never
`private`). The only cross-assembly consumer is the test project, reached via
`InternalsVisibleTo rldump.Tests`. Every class that cannot reasonably be inherited is
`sealed` (the four `JsonSerializerContext` partials included); `static` classes and
`record struct`s are already implicitly sealed. Keeping the surface internal also gives
the Native AOT trimmer more room to eliminate unreachable members.

### 9.1 API models — explicit properties for transformed fields only, rest via extension data

`Output/RldumpJson.cs` hosts the source-generated contexts:

- `ApiJsonContext` — request/response API types (naming policy `null`; C# property names
  equal the wire names exactly).
- `OutputJsonContext` — tidy output types (naming policy `CamelCase`).

**Design principle**: response models declare explicitly **only the fields the tidy output
transforms**. All other wire fields are captured by a `[JsonExtensionData]`
`Dictionary<string, JsonElement>?` property and passed through untouched. If the API adds
a field, it appears in tidy output automatically (camelCased, §9.2) — no code change.
(`Dictionary<string, JsonElement>` is the supported extension-data declaration for source
generation / Native AOT; wrong declaration types raise source-generator diagnostic
SYSLIB1036.)

`GetScheduleRequest { Region }` and `GetScheduleResult { Schedules }` — small, fully
explicit (the `Schedules` items carry the extension data).

`ScheduleEntry` (explicit + extension data):

| C# property (= wire name) | Type | Output transform |
| --- | --- | --- |
| `Time` | `long` | → ISO 8601 UTC string |
| `ScheduleID` | `long` | → string |
| `bUpdateSkill` | `bool` | → `updateSkill` |
| `Tournaments` | `TourSettings[]` | mapped per-tournament |
| `[JsonExtensionData] ExtensionData` | `Dictionary<string, JsonElement>?` | keys → camelCase |

`TourSettings` (explicit + extension data):

| C# property (= wire name) | Type | Output transform |
| --- | --- | --- |
| `ID` | `long` | → string |
| `ScheduleID` | `long` | → string |
| `StartTime` | `long` | → ISO 8601 UTC string |
| `GenerateBracketTime` | `long?` | → ISO 8601 UTC string / omitted |
| `MatchExpireTimestamp` | `long?` | → ISO 8601 UTC string / omitted |
| `bPublic` | `bool` | → `public` |
| `bIsMultiGameMode` | `bool?` | → `isMultiGameMode` / omitted |
| `GameMode` | `int` | → `GameModeName` |
| `GameModes` | `int[]` | → `GameModeName[]` |
| `[JsonExtensionData] ExtensionData` | `Dictionary<string, JsonElement>?` | keys → camelCase — holds `Title`, `CreatorName`, `Region`, `DisabledMaps`, `IsSchedulingTournament`, and any future fields |

- 64-bit fields are `long` (safe in C#; the JS precision concern is handled at output time).
- Fields that may be absent on the wire are nullable (`long?` / `bool?`); the always-present
  ones (`ID`, `ScheduleID`, `StartTime`, `Time`, `bPublic`, `bUpdateSkill`, `GameMode`,
  `GameModes`) are non-nullable per the contract.
- Missing wire fields must not fail deserialization (System.Text.Json default).
- Rule for future work: **never add a pass-through field to a model** just because it was
  noticed — it already flows through. Model a field only when it needs a transform.

### 9.2 Tidy output models and mapping

Top level: `{ "schedules": [ … ] }`.

Output models mirror the input models: transformed properties with new value types, plus
`[JsonExtensionData] Dictionary<string, JsonElement>? ExtensionData`:

- `ScheduleEntryOut`: `Time` (string), `ScheduleID` (string), `UpdateSkill` (bool),
  `Tournaments` (`TourSettingsOut[]`), + extension data.
- `TourSettingsOut`: `Id` (string), `ScheduleID` (string), `StartTime` (string),
  `GenerateBracketTime` (string?), `MatchExpireTimestamp` (string?), `Public` (bool),
  `IsMultiGameMode` (bool?), `GameMode` (`GameModeName`), `GameModes` (`GameModeName[]`),
  - extension data.

Under the `CamelCase` policy these serialize as `time`, `scheduleId`, `updateSkill`,
`tournaments`, `id`, `startTime`, `generateBracketTime`, `matchExpireTimestamp`, `public`,
`isMultiGameMode`, `gameMode`, `gameModes`.

**Null handling**: nullable output properties carry
`[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` — a field that is absent
or null on the wire is **absent** from the output (no invented nulls). Extension data
naturally only contains keys that were present.

**Mapping step** (pure, synchronous function in `Output/ScheduleOutput.cs`):

- Copies transformed values (ID → `long.ToString()`; epoch → ISO string; `bX` → `x`;
  mode index → `GameModeName`).
- Copies extension data with each key passed through
  `JsonNamingPolicy.CamelCase.ConvertName(key)`. This must happen in code because the
  serializer writes extension-data keys **verbatim** (the naming policy does not apply to
  them). CamelCase is idempotent on typical names, so there is no double-conversion risk.
- Output property order: explicit properties in declaration order, then extension-data
  entries in wire order. Consumers must not rely on order.

Example tournament object from the fixture (null/absent fields omitted, pass-through
fields follow):

```json
{
  "id": "53008201",
  "scheduleId": "59935",
  "startTime": "2026-11-05T01:00:00Z",
  "public": false,
  "gameMode": "Dropshot",
  "gameModes": [],
  "title": "3v3 Dropshot",
  "creatorName": "Psyonix",
  "creatorPlayerId": "Steam|0|0",
  "region": "USC",
  "maxBracketSize": 32,
  "rankMin": 0,
  "rankMax": 22,
  "platforms": ["Steam", "PS4", "XboxOne", "Switch", "Epic"],
  "gameTags": "",
  "teamSize": 3,
  "disabledMaps": ["ARC_P", "…"],
  "seriesLength": 1,
  "finalSeriesLength": 3,
  "seriesRoundLengths": [3, 3, 1],
  "seedingType": 2,
  "tieBreaker": 0,
  "teamsRegistered": 0,
  "isSchedulingTournament": true
}
```

Transformations (API → tidy), for reference:

- `ID`, `ScheduleID` → JSON **string** (JS-safe) — done in the mapping step.
- `Time`, `StartTime`, `GenerateBracketTime`, `MatchExpireTimestamp` → ISO 8601 UTC
  (`"…Z"`) strings — mapping step: `DateTimeOffset.FromUnixTimeSeconds(x)` →
  `ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'")` (invariant; no `DateTime`
  round-tripping gotchas).
- `bUpdateSkill` → `updateSkill`, `bPublic` → `public`, `bIsMultiGameMode` →
  `isMultiGameMode` (drop the `b` prefix).
- `GameMode` / `GameModes` → `GameModeName` (§6) — the only transformed value that needs a
  custom `JsonConverter` (string-or-number wire output).
- Everything else: key camelCased, value passed through verbatim (`JsonElement`).

### 9.3 `GameModeName` (string-or-number output type)

`GameModeName` is an `internal readonly record struct` pairing the mode index with a
nullable name — `Name` null means unknown index.

`GameModeNameConverter` writes via `Utf8JsonWriter`: string when the name is known,
number otherwise. Registered in `OutputJsonContext`.

### 9.4 `--raw` mode

Capture the response body as a string; pretty-print (or compact) it and emit verbatim.
Implementation: parse to `JsonElement` (validates JSON), re-serialize with
`JsonNode`/`JsonElement.ToString()` or `Utf8JsonWriter` indented — no reflection, AOT-safe.
Raw's remaining role is a **verbatim wire copy** (PascalCase keys, epoch seconds, numeric
IDs) — new-field loss in tidy mode is already prevented by the extension data (§9.1).

---

## 10. Tests (xunit v3 4.0.2 — all offline)

Test project per §4.4. No test touches the network; HTTP is faked.

### 10.1 `Fakes/FakeHttpHandler.cs`

Subclass of `HttpMessageHandler` that returns canned `JsonDocument`/string responses
keyed by `(method, path)`; records the last `HttpRequestMessage` (method, URL, headers,
body) for request-shape assertions. A "network failure" mode throws
`HttpRequestException` for transport-error tests.

### 10.2 Coverage list

**ConfigLoaderTests**

- cwd `rldump.json` alone; XDG file alone; both → per-field merge, cwd wins.
- `XDG_CONFIG_HOME` respected; falls back to `$HOME/.config` when unset.
- `--config` path used exclusively (other files ignored).
- Missing token → error identifying searched paths. Malformed JSON → error with path +
  position. Unreadable file → exit-3 error. Empty/whitespace token → treated as missing.

**RocketPlanetClientTests**

- Sends method `HttpMethod.Query`, correct URL, `Authorization: Bearer …`, `Content-Type`,
  and body `{"Region":"USW"}` (exact JSON) for GetSchedule.
- Sends `{}` body for GetScheduleRegions.
- Success envelope → returns `Result`.
- Error envelope → `ApiErrorException` with `Type`/`Message`.
- Non-2xx → `ApiHttpException`; response body text with a token-like string is redacted
  in the exception message.
- Timeout → surfaces as transport failure (fake handler delays past a 1s timeout, or a
  `TaskCanceledException` mode).

**GameModeMapTests**

- Embedded resource parses; 33 entries; spot checks (0=Soccar, 5=Dropshot, 27=Pentathlon
  Tournament, 33=Bullet Ball); index 4 absent.

**ScheduleOutputTests** (fixture-driven, using the real wire JSON)

- Full tidy output equals the expected JSON (assert on the serialized string or on
  `JsonNode` equality) — covers camelCase, `b`-prefix drop, string IDs, ISO dates,
  null passthrough, empty arrays.
- Mode names: `5 → "Dropshot"`; `27` + `[3,12,20,1,6] → "Pentathlon Tournament"` +
  `["Rumble","Dropshot Rumble","Super Cube","Hoops","Heatseeker"]`.
- Unknown mode index (use `4` and an out-of-range `999`, via a modified fixture) → raw
  number in output.
- **Extension-data passthrough** (modified fixture): an extra unknown property at the
  tournament level (e.g. `"BracketsUrl": "http://…"`) and at the schedule level
  (e.g. `"Season": 12`) → tidy output contains `"bracketsUrl"` / `"season"` with
  unchanged values, no key is emitted twice, and wire fields absent from the fixture are
  absent from the output (no invented nulls).
- `--raw` output preserves `IsSchedulingTournament` and all PascalCase keys.

**ScheduleCommandTests / RegionsCommandTests** (end-to-end through the command action with
fake handler + temp config)

- Exit code 0 on success; stdout is valid JSON; stderr empty.
- API error → exit 1, message on stderr, token not present in stderr.
- No token → exit 3. Network failure → exit 4.
- `-o FILE` writes JSON to the file and leaves stdout empty.
- `--compact` produces single-line output.
- `regions` prints the `Result` payload (tidy) / full envelope (`--raw`).

**Regression discipline**: the GetSchedule success fixture is a verbatim copy of a
real API response. Do not edit it — it is the ground truth for wire shape.

---

## 11. AOT build and verification

- `mise run publish` → `dotnet publish src/rldump/rldump.csproj -c Release -r linux-x64
  -o dist` (plus `-p:Version=$RLDUMP_VERSION` when that env var is set) →
  self-contained `dist/rldump` (clang from mise is the linker/compiler).
- Binary must run without any .NET installation: `dist/rldump --help` and
  `dist/rldump --version`.
- Smoke (manual, needs a real token in a temp cwd): `dist/rldump schedule --region USW`
  against the live API, and `dist/rldump regions`.
- Watch for trim warnings during `mise run build` (warnings are errors); AOT-specific
  issues (e.g. reflection in a dependency) surface at publish time.

---

## 12. CI — `.github/workflows/pull_request.yml` + `release.yml`

Two workflows, mirroring gravwar's split: `pull_request.yml` runs the full
build/test/publish gate on PRs to `main`; `release.yml` runs the same gate on
pushes to `main` and additionally packages the tarball, uploads the run
artifact, and creates the GitHub release. Pushes to other branches run no CI —
their PRs do (gravwar convention: a push trigger on the PR workflow would
double-run every job on each commit to main).

**`pull_request.yml`** (trigger: PRs to `main`) runs one job: `mise trust`,
`mise install`, then the `mise run` tasks `setup` → `build` → `test` →
`publish`, and the AOT smoke checks — `./dist/rldump --help` and asserting
`--version` reports the stamped `RLDUMP_VERSION`. That's all: no packaging, no
artifact, no release.

**`release.yml`** (trigger: pushes to `main`) runs the same job with three
differences: checkout uses `fetch-depth: 0` (a full clone — the release notes
need the push's before-commit), the job declares `permissions: contents: write`
(the release action's requirement), and a workflow-level `release` concurrency
group queues concurrent runs instead of cancelling them (publish ordering).
After the gate it packages `dist/rldump` + `rldump.json.example` into
`rldump-linux-x64-<version>.tar.zstd`, uploads it as the run artifact
(`archive: false` — the artifact is the tarball itself), writes
`release-notes.md`, and creates the release with
`softprops/action-gh-release@v3` — tag `v<version>`, the notes as the body,
the tarball attached as the asset.

- `jdx/mise-action@v4`, `actions/checkout@v7`, `actions/upload-artifact@v7`,
  and `softprops/action-gh-release@v3` match gravwar's workflow conventions
  (upload-artifact v7.0.2, verified; action-gh-release v3 is the maintained
  line — v2.6.2 is the final v2 and runs on the deprecated Node 20 runtime).
- **Versioning** (gravwar-style): the base is `<Version>` in
  `src/rldump/rldump.csproj` (`1.0.0`); CI keeps major.minor and replaces the patch
  with `GITHUB_RUN_NUMBER`, exposing it as `RLDUMP_VERSION`. The `mise run publish`
  task passes it to `dotnet publish -p:Version=…`, so the AOT binary's
  `--version` and the artifact name both carry exactly `1.0.<run>` (the main csproj
  opts out of the SDK's `+<GITHUB_SHA>` informational-version suffix — §4.3). A
  smoke step asserts the binary reports exactly that version.
- The artifact is a real `rldump-linux-x64-<version>.tar.zstd` file (the `rldump`
  binary + `rldump.json.example`), not a zip: `archive: false` makes upload-artifact
  upload the single file verbatim and use its filename as the artifact name. `zstd`
  is preinstalled on ubuntu-latest; the install guard is just in case.
- **Release** (gravwar-style, `release.yml` only): `softprops/action-gh-release@v3`
  creates a stable release tagged `v<version>` with the tarball attached via
  `files:`. The tag is unique per run (unique version), so no release is ever
  overwritten. Each release is also available as a run artifact under the CI run.
- **Release notes** (gravwar's logic, adapted): GitHub's
  `generate_release_notes` only lists merged PRs and closed issues, which is
  useless for a project that commits directly to main — so the "Write release
  notes" step writes `release-notes.md` itself: the archive reference with a
  `curl` download line, plus a "Commits" section listing the commits in
  `event.before..GITHUB_SHA` (`.before` read from `$GITHUB_EVENT_PATH` with
  `jq`). A zero/missing `before` (branch-creating push) has no usable range and
  lists the release commit alone (`$GITHUB_SHA~1..$GITHUB_SHA`). This is why the
  checkout uses `fetch-depth: 0` — a shallow clone can't resolve the
  before-commit.
- **Publish ordering**: `concurrency: { group: release, cancel-in-progress:
  false }` queues concurrent release runs so publishes land in commit order —
  a stale build that finishes after a newer one must not become "Latest".
  `permissions: contents: write` is what the release action needs (creating the
  release + its tag); the PR workflow needs no extra permissions.
- `mise run setup` (dotnet restore) must run before `build`, which passes
  `--no-restore` — a fresh checkout has no package cache otherwise.
- The AOT publish on ubuntu-latest is the main cost; acceptable at v1. If it becomes slow,
  split publish into its own job later — not now.

---

## 13. Documentation

### 13.1 `AGENTS.md`

Follow gravwar's structure (Stack / Commands / Critical Patterns / Agent Rules):

- **Stack**: C# 14 / .NET 10 single CLI + xunit v3 (4.x, MTP) test project; toolchain via
  mise (`dotnet 10.0.403`, `clang 23.1.4`).
- **Commands**: `mise trust && mise install && mise run setup` first time;
  `mise run build|test|publish`.
- **Critical patterns**:
  - The API's HTTP method is the custom verb **`QUERY`** — never "POST". Use the .NET 10
    built-in `HttpMethod.Query` static property (not `new HttpMethod("QUERY")`).
  - Every response is the `{Error|Result}` envelope — always check `Error` first.
  - AOT: System.Text.Json **source generation only** (the two `JsonSerializerContext`s);
    no reflection-based serialization, no `JsonSerializer.Deserialize<T>()` with a
    runtime type.
  - Tidy output invariants: IDs are strings, timestamps are ISO 8601 UTC, unknown game
    mode indices stay numbers, `--raw` is always verbatim.
  - Response models explicitly declare only the fields the tidy output **transforms**;
    everything else flows through `[JsonExtensionData]` (`Dictionary<string, JsonElement>`).
    Never add a pass-through field to a model because you noticed it — it already passes
    through. Model a field only when it needs a transform. Extension-data keys are written
    verbatim by the serializer, so the mapping step camelCases them itself.
  - Token never printed/redacted defensively; config file is `rldump.json`
    (tracked example: `rldump.json.example`); search order is
    `--config` → `./rldump.json` → XDG.
  - Canonical API reference: the live docs data endpoints
    (`/docs/index.json`, `/docs/contract?service=…`) — consult them before adding
    endpoints; the reflected types can be incomplete (live responses may carry extra
    fields, e.g. `IsSchedulingTournament`).
  - Game-mode map is embedded data extracted from the game binary: replace
    `src/rldump/Data/gamemodes.json` and rebuild when the game updates.
- **Agent rules**: validate third-party library usage with context7 before implementing;
  run `mise run test` after any change; keep the wire fixture verbatim.

### 13.2 `README.md`

- What it is (one paragraph) + that the JSON targets a web page consumer.
- Install: link to the latest GitHub release; pick the archive for one's OS/CPU.
- Build: mise setup lines + `mise run publish`.
- Config: file shape, search order, where to get a token
  (rocketplanet.gg account portal), gitignore note.
- Usage examples for both commands, with the sample tidy output block and a note on
  `gameMode: string | number` semantics.
- Exit code table.
- "Adding an endpoint" pointer (docs data endpoints + envelope + contexts pattern).
- License: MIT (repo-root `LICENSE`; deliberately not bundled in the release
  tarball, which ships only the binary + example config).

---

## 14. Implementation order

1. **Skeleton**: `.gitignore`, `mise.toml`, `global.json`, `rldump.slnx`, both csprojs,
   minimal `Program.cs` (`root --help` works), empty command stubs.
   Verify: `mise trust && mise install && mise run build` (first run downloads
   dotnet+clang).
2. **Config**: `RldumpConfig` + `ConfigLoader` + tests (§10.2 ConfigLoaderTests).
3. **Client**: `RocketPlanetClient`, envelope, exceptions + tests
   (request shape, envelope, error, redaction, timeout).
4. **schedule command**: API models + `ApiJsonContext`, fixture copy, command wiring,
   tidy output models + `OutputJsonContext` + converters + `GameModeMap` +
   `GameModeName`; `--raw/--compact/-o/--timeout/--config`. All schedule/
   output/mode-map tests green.
5. **regions command**: model (opaque `Result` as `JsonElement`), command, tests.
6. **AOT**: `mise run publish`; verify `dist/rldump --help/--version`; fix any
   trim/AOT findings.
7. **Docs + CI**: `AGENTS.md`, `README.md`, `rldump.json.example`, `pull_request.yml` + `release.yml`
   (upload-artifact@v7 confirmed; version = csproj major.minor + run number;
   artifact = `rldump-linux-x64-<version>.tar.zstd`);
   final full pass: `mise run build && mise run test && mise run publish`.

Definition of done: all tests pass via `mise run test`; `dist/rldump` builds and runs
standalone; README + AGENTS.md accurate; repo clean (no untracked build artifacts).

---

## 15. Explicit non-goals for v1 (future work)

- Other endpoints (brackets, results, tournaments, players, …) — the client/context
  design is built to add them incrementally.
- Auth token acquisition/refresh (out of scope; tokens come from the portal).
- `--player-id` for GetSchedule (server currently accepts requests without it).
- Named enums for `SeedingType`/`TieBreaker`/`GameMode`-adjacent ints beyond the
  provided mapping (kept numeric to avoid guessing).
- Multi-region batch queries, caching, polling/watch mode.
- Release workflow (tags, GitHub Releases) — deferred; CI artifact is enough for now.
- Windows/macos AOT builds (linux-x64 only for v1; the csproj is portable).
