*author: Copilot*

# Testing rules

1. If a test exists in both `test/macarena/...` and `test/toddler/...`, all
   requests for that test implicitly refer to the one in `test/macarena/...`.
2. Never delete a test without specific instruction to do so. Even if obsolete
   tests accumulate in `test/toddler/`, they are required for project
   submission.
3. Always generate tests to use the framework in `test/common/`.
4. Macarena tests may mock components to exercise specific test semantics.
   Do not add mocks merely to compensate for missing dependencies or other
   problems that need to be resolved in `src/`.

## Actor work-period rules

5. Scene-tick steps: work proceeds in steps of one scene-tick, and the final
   step is shortened to the remaining time (e.g. 330ms at a 70ms scene-tick is
   four 70ms steps and one 50ms step). Tests check the total, not just
   completion. Always qualify "tick" as scene-tick or resource-tick.
6. Work period vs. resource hold:
   - The work period is `[work: start]` to `[work: stop]`. A scene's work
     period lasts the scene duration (unless `stop_now`), even when a resource
     `minwork` extends the hold.
   - Resource hold time begins at `[work: start]` and ends at the
     `MultiRelease` log entry. A resource granted earlier is "available but
     not held", as if all `Grant()` calls occurred simultaneously.
   - The hold is bounded below by the largest `minwork` and above by the
     smallest `maxwork` of the scene's resources. Conflicting bounds are an
     illegal configuration. `stop_now` overrides `minwork`.
   - At `maxwork` the Actor must release the resources and requeue; the scene
     continues until its full duration has been worked.
7. Tolerance: only scene-tick counts; start and stop latency and duration
   jitter are each within 2 x scene-tick.
8. Waiting for resources is not work: it never counts toward the scene
   duration or any bound, and no work is logged during it.
9. Interrupted scenes: after `stop_now` a restart begins at Scene 0 with a
   fresh full duration; partial progress is discarded. Unless stopped, the
   Actor repeats the scene sequence indefinitely; stopping after one pass is a
   bug.
10. `stop_at_completion` is not implemented; using it must raise an exception.
