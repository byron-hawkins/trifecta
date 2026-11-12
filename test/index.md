*author: Copilot*

# Test index

Tests are grouped by suite and source file. Each entry summarizes the exercise and the main assertions; parameterized
tests run several cases, and the linked source gives their full details. Timing tests allow for scheduling tolerance.
See the [README](../README.md#running-the-tests) for prerequisites and the distinction between suites.

## Macarena: component tests

These tests exercise a real component with controlled inputs or neighboring components replaced by mocks.

### Environment

#### [SensorControllerTests](macarena/trifecta/environment/SensorControllerTests.cs)

Checks the sensor controller's lifecycle with configured drivers.

- **StopsPromptlyAfterRegisteringBothSensors** — Registers active pressure and passive temperature drivers, starts
  and stops the controller, and checks that shutdown completes within two seconds; the case runs twice.

#### [SensorInterpreterTests](macarena/trifecta/environment/SensorInterpreterTests.cs)

Checks stored sensor values, threshold detection and notifications.

- **NotifiesAwareWhenTemperatureOrPressureCrossesAThreshold** — Sends generated temperature and pressure readings
  across randomized thresholds and compares listener notifications with the expected crossings.
- **SensorUpdatesCanBeReadBackAndAreLogged** — Sends both kinds of update with trace enabled and checks the current
  values and the logged sensor IDs and readings.

### Logging

#### [ComponentLogTests](macarena/trifecta/io/ComponentLogTests.cs)

Checks basic file logging.

- **WritesSingleMessageToLog** — Writes and flushes one message and checks that the log file contains it.

#### [ComponentLogConcurrencyTests](macarena/trifecta/io/ComponentLogConcurrencyTests.cs)

Checks attribution and completeness when multiple tasks share the log.

- **LogsMessagesFromFourRegisteredTasks** — Four registered tasks each write 3–5 messages; the test checks that each
  message and each release appears exactly once under the correct component name.

### Resources

#### [ResourceQueueGhostTests](macarena/trifecta/resource/ResourceQueueGhostTests.cs)

Exercises queues directly with live and abandoned ("ghost") consumers to check that resources are not lost.

- **ReleaseSkipsGhostAtHeadAndGrantsLiveConsumerBehindIt** — Returns a held resource with a ghost and a live consumer
  waiting, and checks that only the live consumer is granted it.
- **ReleaseWithOnlyGhostsWaitingKeepsTheResource** — Returns a resource past a ghost and checks that a later live
  consumer can acquire it.
- **ReleaseSkipsSeveralGhostsInARow** — Returns a resource past four ghosts and checks that the live consumer receives
  it and the ghosts do not.
- **GhostsInterleavedWithLiveConsumersKeepFifoOrderOfLiveOnes** — Queues two live consumers around a ghost and checks
  their grant order as the resource is returned.
- **GhostWaitingWithNoLiveConsumerNeverConsumesAResourceAcrossReleases** — Returns two resources past ghosts and
  checks that two distinct instances remain available.
- **GhostPushedAsConsumerIsIgnoredAndLeavesResourceAvailable** — Pushes an already-ghost consumer and then a live one,
  checking that only the live consumer receives the resource.
- **ConsumerBecomingGhostBeforeDispatchIsSkippedWithoutLosingResource** — Abandons a waiting request before release
  and dispatch, then checks that the live consumer receives the resource.
- **ResourceCountIsConservedThroughManyGhostChurnRounds** — Repeats 20 ghost-and-reacquisition rounds with three
  resources and checks that the released instance can be acquired again each time.

#### [ResourceQueueSetConfigurationTests](macarena/trifecta/resource/ResourceQueueSetConfigurationTests.cs)

Checks resource configuration loading and queue creation logs.

- **ResourceWorkBoundsDeserializeAsMilliseconds** — Loads two resource types and checks their deserialized minimum and
  maximum work bounds (50 ms and 1,000 ms).
- **ConfigureLogsEachResourceType** — Configures the queue set from a file and checks that the resource types are
  recorded in the log.

#### [ResourceQueueSetTests](macarena/trifecta/resource/ResourceQueueSetTests.cs)

Checks resource requests, releases and sharing under varied demand.

- **SingleResourceIsGrantedThenReleasedBackToItsQueue** — Requests and returns one resource, checking its identity
  and the order of receipt, grant, return and re-enqueue log entries.
- **TwoActorsCompeteForOneResource** — Varies two hold times and the second request's arrival, checking grant order,
  waiting and non-overlapping holds.
- **ResourceExchangesFollowGrantAndReturnSequence** — Exchanges two resource types among three consumers in both
  type orders, checking the detailed queue log sequence and the identities of returned and reacquired instances.
- **ActorsShareMultipleInstancesOfOneResource** — Runs mock actors through repeated requests with varying demand,
  capacity and hold times, checking completion, capacity, FIFO timing and returns.
- **ActorsShareSeveralResourceTypes** — Runs mixed requests across several resource types, checking per-type capacity,
  timing, contention and resource returns.

### Simulation

#### [ConfigurationFileLoaderTests](macarena/trifecta/simulation/ConfigurationFileLoaderTests.cs)

Checks that sensor JSON fixtures deserialize into the intended models.

- **SensorJsonFilesDeserializeIntoTheirModels** — Loads a device and script and checks their IDs, mode, tick interval,
  event durations and values.
- **LoadsTemperaturePassiveAscendingScript** — Loads the ascending temperature script and checks its two event
  durations and values.

#### [ScriptedActiveSensorDriverTests](macarena/trifecta/simulation/ScriptedActiveSensorDriverTests.cs)

Checks scripted readings and listener/driver lifecycle behavior; sequence tests use a 40 ms sensor tick.

- **TemperatureRisesAndFallsBetweenTwoEvents** — Runs a two-event temperature script and checks interpolated readings,
  sensor ID, tick spacing and that reporting stops.
- **TemperatureSlidesOverUnevenSegmentsNotAlignedToTicks** — Runs three uneven temperature segments and checks the
  reading sequence across segment boundaries.
- **PressureRisesAndFallsBetweenTwoEvents** — Runs a two-event pressure script and checks interpolated integer readings,
  sensor ID and tick spacing.
- **PressureSlidesOverUnevenSegmentsNotAlignedToTicks** — Runs three uneven pressure segments and checks readings across
  their boundaries.
- **ConstantScriptRepeatsTheSameValue** — Runs a constant temperature script and checks six identical readings.
- **EveryAttachedAwareReceivesReadingsUntilDetached** — Detaches one of two listeners while running and checks that
  its readings stop while the other continues.
- **EmptyScriptNeverReports** — Starts an empty pressure script and checks that no readings arrive.
- **StopWithoutStartIsHarmless** — Stops an unstarted driver and checks that the call succeeds.
- **DriverReportsAgainAfterStopAndRestart** — Stops and restarts a driver and checks that new readings arrive.
- **DetachFromInsideACallbackDoesNotBlockOrThrow** — Detaches a listener from its callback and checks that it is
  called once while another listener continues.
- **AttachAndDetachDuringARunNeverThrowOrLoseTheStableListener** — Churns 20,000 listeners during a run and checks
  that a stable listener continues receiving readings.
- **StopFromInsideACallbackDoesNotDeadlockAndAllowsRestart** — Stops from a callback, then restarts the driver and
  checks that reporting resumes.

#### [SensorDriverFactoryTests](macarena/trifecta/simulation/SensorDriverFactoryTests.cs)

Checks factory construction from configuration paths.

- **CreatesScriptedDriverFromRelativeConfigurationPaths** — Creates a scripted driver from relative device and script
  paths and checks that it is returned.

### Stage

#### [ActorTests](macarena/trifecta/stage/ActorTests.cs)

Checks actor construction, scene work, resource timing, restart and interruption.

- **RequestsAndReleasesResourceDuringSceneWork** — Grants a requested resource and stops the actor during work;
  checks the request and return of the same instance.
- **CompletesResourceSceneForItsFullDuration** — Runs a one-second resource scene and checks completion, work duration
  and release.
- **ResourceWorkUnderflowHoldsResourceUntilMinimumWork** — Runs a 200 ms scene with a 600 ms resource minimum and
  checks scene work and extended resource hold.
- **StartsAndStopsDuringLocalSceneWork** — Starts and interrupts a scene without resources and checks the work log.
- **CompletesLocalSceneForItsFullDuration** — Runs a one-second local scene and checks completion and elapsed work time.
- **RestartsAndRepeatsLocalSceneSequence** — Runs and restarts a two-scene local sequence, checking ordering and
  repetition.
- **RestartsAndRepeatsUnevenLocalAndResourceSceneSequence** — Uses 200 ms and 330 ms scenes with a 70 ms scene-tick;
  checks restart, repetition and resource requests and releases.
- **RequestsAndReleasesTwoAndThenThreeResourcesInSuccessiveScenes** — Runs consecutive multi-resource scenes and
  checks that each requests and releases its configured resource set.
- **ReleasesAndRequeuesAtLowestMaxWork** — Varies scene duration and resource maximums, checking hold lengths,
  requeueing and total coverage of scene work.
- **StartsWorkOnlyAfterDelayedGrantsArrive** — Delays grants in several scene sequences and checks that work starts
  after the last grant, with expected work lengths and releases.
- **StopDuringMultiPushIsNotOverriddenByLaterGrant** — Stops while a three-resource request is in progress, varying
  how many grants arrived first; checks that work does not start and granted resources are released.
- **GrantAfterActorExitsDoesNotThrowOrLoseResource** — Offers a resource after an actor has stopped and checks that
  the offer sees a ghost, does not throw and does not start work.
- **ConstructionLogsRoleAndEachScene** — Constructs an actor with configured scenes and checks the construction log.

#### [DirectorTests](macarena/trifecta/stage/DirectorTests.cs)

Checks director configuration and lifecycle logging.

- **ConfigureLogsRolesAndConstructedScenes** — Configures multiple roles and checks the actor and scene construction
  log entries.
- **StartsGoesIdleAndStopsPromptly** — Starts an empty director and stops it in both stop modes, checking idle and
  lifecycle logs and bounded shutdown.

#### [SceneTests](macarena/trifecta/stage/SceneTests.cs)

Checks scene construction logging.

- **ConstructionLogsDurationAndRequiredResources** — Constructs a scene and checks its duration and required resources
  in the log.

## Ensemble: integration tests

These tests connect real components; the boundary and cast-observation cases use recording actor subclasses.

### Environment

#### [SensorControllerTests](ensemble/trifecta/environment/SensorControllerTests.cs)

Checks the sensor-to-interpreter-to-director path using active scripted drivers.

- **ActiveSensorsCrossThresholdsAndNotifyListenerOnlyOnCrossing** — Runs ascending temperature and pressure scripts
  across three thresholds each, checking crossing values and listener notifications against the log.
- **DirectorIsToldOnSensorThreadsAndOptimizesOnItsOwnThread** — Runs both drivers through threshold crossings and
  checks the thread attribution of director notifications and optimization.
- **CastChangesRunOnTheDirectorThreadAsSensorsCrossLimits** — Drives crossings through two role configurations and
  checks cast transitions and the threads performing actor changes.

#### [ThresholdBoundaryTests](ensemble/trifecta/environment/ThresholdBoundaryTests.cs)

Checks interpreter and director behavior exactly at role bounds.

- **BoundaryTouchLeavesTheRoleConsistentWithItsBounds** — Moves temperature or pressure to a bound and either reverses
  or crosses it, checking role state at each step and crossing counts for reversals.

### Stage and resources

#### [ActorResourceQueueTests](ensemble/trifecta/stage/ActorResourceQueueTests.cs)

Checks real actors executing scenes against a real shared queue set.

- **ActorsCompleteTheirScenesOnSharedResources** — Varies three scene shapes, one or two actors, and one or two
  resource types and instances; checks scene timing, capacity, FIFO timing and returns.

## Toddler: original stub-based tests

These are the initial TDD tests retained for reference and submission; they exercise stubs rather than the current
implementation, so a listed assertion is not a claim that the suite passes against those stubs.

### Environment

#### [SensorControllerTests](toddler/environment/SensorControllerTests.cs)

Checks the original sensor-controller logging expectations.

- **StartLogsScriptValuesAtTheConfiguredTickRate** — Starts a configured scripted sensor and checks its logged reading
  sequence against values calculated from the fixture and tick interval.

#### [SensorInterpreterTests](toddler/environment/SensorInterpreterTests.cs)

Checks the original interpreter notification and logging expectations.

- **NotifiesAwareWhenTemperatureOrPressureCrossesAThreshold** — Sends generated readings and compares notifications
  with expected threshold crossings.
- **SensorUpdatesCanBeReadBackAndAreLogged** — Sends temperature and pressure updates and checks stored values and logs.

### Simulation

#### [ConfigurationFileLoaderTests](toddler/simulation/ConfigurationFileLoaderTests.cs)

Checks the original sensor configuration and script loading expectations.

- **SensorJsonFilesDeserializeIntoTheirModels** — Loads device and script fixtures and checks their fields and values.
- **LoadsTemperaturePassiveAscendingScript** — Loads the ascending temperature script and checks its event durations
  and values.

#### [SensorDriverFactoryTests](toddler/simulation/SensorDriverFactoryTests.cs)

Checks the original scripted-driver factory expectation.

- **CreatesScriptedDriverFromRelativeConfigurationPaths** — Supplies relative fixture paths and checks that a driver
  is created.

### Logging

#### [ComponentLogTests](toddler/trifecta/io/ComponentLogTests.cs)

Checks the original single-message file-log expectation.

- **WritesSingleMessageToLog** — Writes a message and checks that it appears in the log file.

### Resources

#### [ResourceQueueSetTests](toddler/trifecta/resource/ResourceQueueSetTests.cs)

Checks the original queue-set lifecycle expectation.

- **StartsBumpsFiveTimesAndStopsPromptly** — Starts the queue set, bumps it five times and checks its log and timely
  shutdown.

### Stage

#### [ActorTests](toddler/trifecta/stage/ActorTests.cs)

Checks the original actor construction-log expectation.

- **ConstructionLogsRoleAndEachScene** — Constructs an actor and checks its role and scene details in the log.

#### [DirectorTests](toddler/trifecta/stage/DirectorTests.cs)

Checks the original director configuration-log expectation.

- **ConfigureLogsRolesAndConstructedScenes** — Configures roles and scenes and checks their construction log entries.

#### [SceneTests](toddler/trifecta/stage/SceneTests.cs)

Checks the original scene construction-log expectation.

- **ConstructionLogsDurationAndRequiredResources** — Constructs a scene and checks its duration and resources in the log.
