# AGENTS.md

This file provides guidance to agents when working in this repository.

## Stack

C# 14 / .NET 10. Two projects:

- `src/rldump` — a single Native AOT CLI (`rldump`). It queries the Rocket League
  "Prime" API (`prime.rocketplanet.gg`) and prints JSON for a future web-page consumer.
- `tests/rldump.Tests` — xunit v3 (4.x) test project running on the Microsoft
  Testing Platform (MTP) runner.

Toolchain is managed by [`mise.toml`](mise.toml): `dotnet 10.0.403` and
`clang 23.1.4` (the AOT compiler). CI consumes the same config via
`jdx/mise-action`.

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
- **Tidy-output invariants** (non-`--raw` only): IDs are **strings** (they exceed JS
  safe-integer precision), timestamps are **ISO 8601 UTC**
  (`yyyy-MM-ddTHH:mm:ssZ`), unknown game-mode indices stay **numbers**, and
  `--raw` is always verbatim.
- **Model only what you transform.** Response models declare explicitly only the
  fields the tidy output *transforms*; every other wire field flows through
  `[JsonExtensionData]` (`Dictionary<string, JsonElement>`). Never add a pass-through
  field to a model because you noticed it on the wire — it already passes through.
  Model a field only when it needs a transform. Extension-data keys are written
  **verbatim** by the serializer (the naming policy does not apply to them), so the
  mapping step (`ScheduleOutputMapper.CamelCaseKeys`) camelCases them itself.
- **Game-mode mapping** is embedded data extracted from the game binary:
  `src/rldump/Data/gamemodes.json` (indices 0–3, 5–33; index 4 is absent). Known
  index → name string; unknown index → raw number. When the game ships a new build,
  replace that file and rebuild.
- **Token handling**: the bearer token is read from config only and must **never**
  appear in any output or log — defensive redaction strips the configured token
  value from any message built from response text. Config search order is
  `--config <PATH>` → `./rldump.json` → `$XDG_CONFIG_HOME/rldump.json`
  (fallback `~/.config/rldump.json`), with per-field merge (cwd wins). A tracked
  example ships as `rldump.json.example`; the real `rldump.json` is gitignored.
- **Canonical API reference** is the live docs data endpoints
  (`/docs/index.json`, `/docs/contract?service=…`). Consult them before adding an
  endpoint; the reflected response types can be incomplete (live responses may carry
  extra fields, e.g. `IsSchedulingTournament` — which is exactly what the
  extension-data path exists to absorb).
- **The wire fixture is ground truth**: `tests/rldump.Tests/Fixtures/getschedule-success.json`
  is a verbatim copy of a real curl response. Do not edit it; derive test
  expectations from it.

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
