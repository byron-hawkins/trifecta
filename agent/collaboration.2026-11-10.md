*author: Copilot*

# Collaboration transcript — 2026-11-10

## Summary

Work updated the stage SMD for two new resource work-bound fields, expanded
macarena singleton tests to support repeated runs in one process, added
Director and Actor lifecycle coverage, and discussed operating requirements
for Actor scene execution and timestamped test logs.

## Decisions and design

- `ResourceType.minwork` and `maxwork` are `TimeSpan` values represented in
  JSON as integer milliseconds. The resource fixture already uses `min-work`
  and `max-work`; the loader therefore needs member-name mappings as well as
  the shared milliseconds converter.
- Macarena tests using the `ResourceQueueSet`, `Director`, or
  `SensorController` singleton call `Reset()` before use. The toddler stubs
  gained corresponding `Reset()` members where needed by copied tests.
- Macarena tests may mock components for test semantics, such as controlled
  resource availability. Mocks must not be used merely to work around missing
  dependencies or problems that need resolution in `src/`.
- Singleton-dependent lifecycle tests are run together to verify they can
  execute sequentially within one test process. Component-log tests remain in
  the non-parallel collection.
- Director lifecycle coverage checks that it starts, goes idle, and stops
  promptly for both `Stop(true)` and `Stop(false)`.
- The Actor work requirements agreed for future macarena and ensemble tests
  are:
  1. Resource occupancy is normally bounded by that resource's `minwork` and
     `maxwork`. `ActorPhase.stop_now` overrides `minwork`, so a hold shorter
     than 1 ms is allowed on that path.
  2. Use the Scene's `tickrate` for the Actor stop responsiveness bound:
     a working Actor should stop within two ticks, except when asked to stop
     at completion.
  3. Once an Actor has received its final requested resource, it should begin
     work immediately, subject to the agreed test tolerance.
  4. Every Scene in an Actor's sequence must eventually complete its full
     duration before the Actor advances. Work may be split by `maxwork`.
     `stop_now` removes the completion requirement.
- Work-segment start/end log entries are expected to identify the Actor ID
  and index, the Scene's sequence index, and a timestamp. Timestamps are
  captured at `ComponentLog.Log()` entry, outside the logger lock. A readable
  UTC timestamp with millisecond formatting is acceptable for this demo;
  timing jitter is tolerated. These requirements have been discussed but a
  shared machine-readable catalog and reusable trace evaluator have not yet
  been implemented.
- `TimeSpan` may be a field of a C# struct; fields are zero-initialized when
  their containing value is initialized. Locals must be assigned before use.
- For simulated uninterruptible computation with a 10–20 ms state-check
  interval, use a short `Thread.Sleep()` chunk rather than `Thread.SpinWait()`,
  which consumes CPU and does not specify a duration in milliseconds.

## Outputs and changes

- `doc/architecture/bin/stage.smd` — added `ResourceType.minwork` and
  `ResourceType.maxwork` to match `doc/architecture/stage.md`.
- `meta/generate/serialization-types.json` — added mappings for `min-work`
  and `max-work` to the corresponding `TimeSpan` members.
- `temp/resource-work-json-options.patch` — patch for the read-only source
  loader to map those JSON names. The patch was checked for applicability and
  verified against the real configuration model in an isolated build.
- `test/macarena/trifecta/resource/ResourceQueueSetConfigurationTests.cs` —
  added deserialization checks that both resource bounds load as milliseconds.
- Macarena queue, Director, and SensorController tests now reset their
  singleton before use. The ensemble SensorController test also stops the
  controller after the reading attempt so it does not remain running.
- `test/macarena/trifecta/stage/DirectorTests.cs` — updated expectations for
  indexed Actor log messages and added start/idle/stop checks for both Director
  stop modes.
- `test/macarena/trifecta/stage/SceneTests.cs` and `ActorTests.cs` — updated
  direct constructor calls to pass the new indices. `ActorTests.cs` also has a
  local-scene start/stop test using a one-second scene with no resources.
- Toddler ResourceQueueSet and Director stubs gained `Reset()` methods to
  support their corresponding tests.
- `test/ensemble/trifecta/environment/SensorControllerTests.cs` — added
  singleton reset and controller cleanup.

## Validation and current status

- The ResourceQueueSet configuration tests passed after the loader mappings
  were available. The added assertions verified 50 ms `minwork` and 1000 ms
  `maxwork` values for both fixture entries.
- Seven focused macarena singleton tests passed together; after making the
  SensorController case repeatable, all eight focused cases passed together
  in one process. This included the configuration tests, queue lifecycle,
  Director configure and lifecycle checks, and two SensorController runs.
- The toddler project built successfully with the new reset stubs.
- The ensemble SensorController test still failed: it expected 61 readings
  but observed zero. The test did complete its controller stop cleanup.
- The Actor lifecycle test first timed out waiting for a work-start log
  because `Actor.Start()` set `_onstage` before launching a worker whose
  `ready` phase expects `_onstage` to be null. After the source was changed,
  the test reached `[work: start]`, but its subsequent stop timed out while
  `Actor.Stop()` was still empty. A later rerun was started after further
  source changes, but its result was not captured; do not report the Actor
  lifecycle test as passing until rerun and verified.
- The macarena stage tests passed after updating the Director assertions for
  indexed Actor construction logs.

## Follow-up notes

- Rerun `ActorTests.StartsAndStopsDuringLocalSceneWork` after the latest
  `Actor.Stop()` implementation and inspect the resulting log on failure.
- Consider recording the agreed Actor operating requirements in a stable
  machine-readable catalog and implementing a shared timestamp-trace
  evaluator that generated macarena and ensemble tests can reuse.
- The timestamped work-segment log contract must be added to the source
  implementation before trace-based work-bound and scene-completion
  evaluations can be generated.
- Keep this file listed in `agent/agent-bootstrap.md` as session-essential
  collaboration history.
- Deferred: static lock-semantics / deadlock-safety audit of Actor, Scene,
  Director, and SensorController (lock-order table, call-outs made while a
  lock is held, Enter/Exit pairing, Wait/Pulse use). Wait until the pending
  implementation work lands. Known candidate: Actor holds `_idle` across
  `ResourceQueueSet.MultiPush`/`MultiRelease`, while the queue set will call
  back into `Actor.Grant()`.
