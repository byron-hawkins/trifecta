*author: Copilot*

# Collaboration transcript — 2026-11-09

## Summary

Work continued on JSON configuration for simulated sensors, generic file
loading, architecture SMD, and the toddler-to-macarena test workflow. A
temperature device configuration and script provide editable fixtures for
testing the loader, sensor driver factory, and controller.

## Decisions and design

- Configuration inputs are JSON. Sensor configuration uses `id`, `name`,
  `tick-interval`, `type`, and `mode`; scripts contain an `events` array
  with `duration-ms` and `value` entries.
- `meta/generate/serialization-types.json` defines global JSON-to-C# mappings
  for generated parsers. Integer `duration-ms` and `tick-interval` values
  convert to `TimeSpan` in milliseconds; enum strings are matched by name,
  case-insensitively. Public mapped field names have no leading underscores.
- The architecture Markdown is the source of truth for visibility. When
  serialization requires public fields, mark them `public` there and give
  them names without leading underscores.
- The generic `ConfigurationFileLoader.parse<T>(string path)` uses
  `System.Text.Json.JsonSerializerOptions`. `ParserJsonOptions` in the source
  applies member mappings and converters, rather than relying on an ASP.NET
  Core `JsonOptions` type.
- `SensorConfiguration` is nested in `SensorDriverFactory`, and `Event` is
  nested in `SensorScript<T>`. The factory takes configuration paths relative
  to the directory named by `TRIFECTA_CONFIG_DIR`.
- Tests remain subject to `meta/test/testing-rules.md`: macarena takes
  precedence when the same test exists in both suites, and tests are not
  deleted without specific instruction.

## Outputs and changes

- `configuration/test/sensors/device/temperature-passive.json` — temperature
  sensor ID 103, passive mode, and a 50 ms tick interval.
- `configuration/test/sensors/script/temperature-ascending.json` — two events:
  10.0 for 3000 ms, followed by 50.0 for 1000 ms.
- `meta/generate/serialization-types.json` and
  `meta/generate/csharp-code-generation-policy.md` — mappings and conversion
  rules used by generated JSON parsing code.
- `doc/architecture/simulation.md` and `doc/architecture/bin/simulation.smd`
  — factory rename to `SensorDriverFactory` and public, underscore-free
  mapped configuration and event fields. The relevant SMD declarations were
  synchronized directly; no runnable architecture generator was found.
- `doc/architecture/bin/environment.smd` — synchronized with the
  `SensorController.Start()` operation in the Markdown outline.
- `test/toddler/trifecta/simulation/SensorDrivers.cs` — stubs for the factory
  and nested configuration, script and nested event, and three scripted
  driver types. Existing toddler tests were retained.
- `test/toddler/trifecta/environment/SensorController.cs` and
  `test/toddler/environment/SensorControllerTests.cs` — controller stub and
  test for script interpolation using relative fixture paths and the shared
  logging test base. The test expects 61 readings from 10.0 to 50.0 at 50 ms
  intervals over the first event's 3000 ms duration.
- `test/macarena/trifecta/simulation/ConfigurationFileLoaderTests.cs` — copy
  of the loader tests, adapted to the source models and the current fixture
  paths. The macarena project links the simulation source files. A temporary
  macarena model fixture was removed at the user's request; tests must use the
  source classes rather than recreate that fixture.
- `temp/json-options.cs` — generated example `ParserJsonOptions`; the source
  loader has since incorporated options code separately. The temporary example
  reflects the earlier underscored member names.

## Validation and current status

- The toddler project built after regenerating the sensor driver stubs, with
  three pre-existing unused-field warnings from the logger stub.
- The loader tests previously passed against temporary toddler-derived models.
  After switching to the new source models, the project built but both tests
  failed: the configuration enum converter was attached to `_type` instead of
  `type`, and the script contained zero events. The user subsequently corrected
  the parser's mapped field names; the focused tests have not been rerun since
  that correction. Do not treat the earlier passing fixture-based run as
  validation of the current source models.
- At the time of this summary, `SensorScript<T>._events` in the source is
  private, while `Events` is a read-only view. Deserialization of the fixture
  events still needs verification against the actual source implementation.
- Some prior test invocations could not locate `xunit.execution.dotnet.dll` in
  the existing output directory. Running with an isolated `--artifacts-path`
  successfully restored test discovery.
- For configuration tests, set `TRIFECTA_CONFIG_DIR` to the top-level
  `configuration/` directory. Logging tests may also require
  `TRIFECTA_LOG_DIR`.

## Follow-up notes

- Rerun the focused macarena loader tests after the latest parser correction,
  and report any remaining failure without replacing source models with
  one-time fixtures.
- Progressively move the factory and controller tests to macarena as their
  source implementations become available. Preserve the toddler tests.
- Keep this summary and other session-essential documents listed in
  `agent/agent-bootstrap.md`.
