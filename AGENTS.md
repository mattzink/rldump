# AGENTS.md

This file provides guidance to agents when working in this repository.

## Stack

C# 14 / .NET 10. Two projects:

- `src/rldump` — a single Native AOT CLI (`rldump`). It queries the Rocket League
  "Prime" API (`prime.rocketplanet.gg`) and prints JSON for a future web-page consumer.
- `tests/rldump.Tests` — xunit v3 (4.x) test project running on the Microsoft
  Testing Platform (MTP) runner.

Toolchain is managed by [`mise.toml`](mise.toml): `clang 23.1.4` (the AOT
compiler). The .NET SDK version lives in [`global.json`](global.json) — mise
reads it via its idiomatic-version-file setting, and CI's
`actions/setup-dotnet` reads the same file on Windows, where mise is not used.
Linux CI consumes the mise config via `jdx/mise-action`.

## Commands

- **First-time setup**: `mise trust && mise install && mise run setup`
  (`setup` restores NuGet packages).
- Build (Release): `mise run build`.
- Test: `mise run test`.
- Publish the AOT single-file executable to `dist/rldump`: `mise run publish`.
- Smoke-check the published binary: `./dist/rldump --help` and
  `./dist/rldump --version`.

Run `mise run test` after any code change. The full gate is
`mise run build && mise run test && mise run publish`.

## Critical Patterns

- **The API's HTTP method is the custom verb `QUERY`** — never "POST". Use the
  .NET 10 built-in `HttpMethod.Query` static property (not
  `new HttpMethod("QUERY")`). See `RocketPlanetClient`.
- **Every response is the `{Error | Result}` envelope** — always check `Error`
  first; a non-null `Error` is an `ApiErrorException`. A non-2xx HTTP status is
  an `ApiHttpException`.
- **AOT: System.Text.Json source generation only.** The two `JsonSerializerContext`s
  (`ApiJsonContext` for the wire, `OutputJsonContext` for the tidy output) are the
  only serialization path. No reflection-based serialization, no
  `JsonSerializer.Deserialize<T>()` with a runtime type. `--raw` mode passes the
  verbatim envelope through `JsonElement`, which is AOT-safe.
- **Keep the surface `internal` and sealed.** `rldump` is an `Exe` with no public
  API: every top-level type in `src/rldump` is `internal` (top-level types can't be
  `private` in C# — only `public`/`internal`), reached by the test project via
  `<InternalsVisibleTo Include="rldump.Tests" />` in the csproj. Seal every class
  that can't be inherited (the `JsonSerializerContext` partials included); `static`
  classes and `record struct`s are implicitly sealed. Members of internal classes
  stay `public` (they're effectively internal anyway).
- **Tidy-output invariants** (non-`--raw` only): IDs that exceed JS
  safe-integer precision are **strings**, timestamps are **ISO 8601 UTC**
  (`yyyy-MM-ddTHH:mm:ssZ`), `b`-prefixed wire fields lose the prefix, and raw
  index fields (e.g. game modes, playlists) always stay **numbers** with the
  human-readable name added **alongside** them, never replacing the wire
  value; an index the name mapping doesn't know gets the string `"Unknown"`.
  `--raw` is always verbatim. Endpoints whose fields need no value transform
  print the `Result` payload with all object keys recursively camelCased
  (values verbatim). The concrete name keys and per-command flows are
  documented in the output models, the mappers, and `Output/TidyJson.cs`.
- **The command tree mirrors the API's service layout**: endpoints are grouped
  under a group command, which has no action of its own — a bare group is a
  usage error (exit 2). Global options are defined on the root, so they must
  be written before the first subcommand (`rldump --raw <group> <subcommand>
  …`). Commands with auxiliary lookups (resolving a name, fetching names for
  raw indices) make them in tidy mode only; `--raw` skips them and prints the
  command's own envelope verbatim.
- **Model only what you transform.** Response models declare explicitly only the
  fields the tidy output *transforms*; every other wire field flows through
  `[JsonExtensionData]` (`Dictionary<string, JsonElement>`). Never add a pass-through
  field to a model because you noticed it on the wire — it already passes through.
  Model a field only when it needs a transform. Extension-data keys are written
  **verbatim** by the serializer (the naming policy does not apply to them), so the
  shared mapping helper (`TidyJson.CamelCaseKeys`) camelCases them itself —
  recursively, including nested objects, since extension values also serialize
  verbatim.
- **Name mappings** either embed game data — a new game build then requires
  updating the data file and rebuilding — or are fetched from the live API
  (no rebuild). Each mapping's code says which applies and where the data
  comes from.
- **Token handling**: the bearer token is read from config only and must **never**
  appear in any output or log — defensive redaction strips the configured token
  value from any message built from response text. Config search order is
  `--config <PATH>` → `./rldump.json` → `$XDG_CONFIG_HOME/rldump.json`
  (fallback `~/.config/rldump.json`), with per-field merge (cwd wins). A tracked
  example ships as `rldump.json.example`; the real `rldump.json` is gitignored.
- **Canonical API reference** is the live docs data endpoints
  (`/docs/index.json`, `/docs/contract?service=…`). Consult them before adding an
  endpoint; the reflected response types can be incomplete (live responses may carry
  extra fields — which is exactly what the extension-data path exists to absorb).
- **The wire fixtures are ground truth**: every
  `tests/rldump.Tests/Fixtures/*-success.json` is a verbatim copy of a real
  curl response. Do not edit them; derive test expectations from them. The
  `*-tidy-expected.json` files are expected tidy output regenerated through
  the real code path when a transform changes.

## Agent Rules

### Library Validation

- Validate third-party library/framework/SDK usage with the **context7** tool before
  implementing. Never assume API signatures — verify against up-to-date documentation.
  (Notably, `System.CommandLine` 2.0.x has a different API surface than the 2.0
  beta/preview line the web docs often show.)

### Test Execution

- After ANY code change, run `mise run test`. Tests MUST pass — debug and fix root
  causes; never finish with failing tests.
- Keep the wire fixture verbatim and keep new expectations derived from it rather than
  hardcoded where possible.

### AOT Discipline

- After changing the request/response models or adding a type to a serialization
  path, run `mise run publish` and smoke-check `./dist/rldump --help` to catch any
  trim/AOT breakage before calling the work done.

### Output Contract

- JSON always goes to **stdout** (or `-o FILE`); human-readable diagnostics always go
  to **stderr**. Never mix the two. Exit codes: 0 success, 1 API error, 2 usage,
  3 configuration, 4 network/timeout.
