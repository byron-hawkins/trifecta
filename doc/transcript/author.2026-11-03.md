*Author: Byron*

*Subject: identify cases in both the customer experience and system behavioral capability*

## Elaborating the base criteria

**Context:** taken from the [exercise instructions](/doc/architecture/SRE_Evaluation_Ver.2.0.pdf) provided by Aydin

A number of key factors about behavioral capability remain in question about the system under development:

1. Sensors
    1. Although only a single measure each for temperature and pressure, at any given point in time, are mentioned
       in the stage map, plural is used in the parts list, e.g.: "temperature sensors". This suggests a system in
       which one logical temperature is concluded at time t on the basis of potentially many temperature sensors
       (and the same for pressure). To account for unknowns, introduce flex point (1) "sensor interpretation".
    2. Interface details such as the communication mechanism and value representation are not specified. For
       example, it is not known whether a sensor (a) provides a driver for the software host, (b) performs push
       notification of changes, or (c) transforms hardware receptor signals into temprature units. It is also not
       known whether sensors can differ in these respects: can multiple sensors with differing operate simultaneously,
       each with a differing output mechanism? Introduce flex point (2) "sensor bridge".
    3. Sensors may be added and replaced, but no states are designated for sensors. Suppose for example, factory
       conditions permit only replacement of all sensors together: there will be some period of sensor unavailability,
       which the software must respond to. Introduce flex point (3) "sensor availability".
2. Stage Map
    1. Conditions for running a stage are not mutually exclusive. Introduce flex point (4) "stage concurrency".
    2. Nothing is stated about conditions where (Temperature <= 5.0 || Pressure >= 100). Introduce assumption
       (1) "idle until conditions improve"
    3. Requirements do not specify a strategy for responding to a condition change that crosses a stage threshold,
       nor is the significance of conditions explained. It is possible that serious danger arises from running a
       stage outside the designated conditions zone. It is equally possible that a high cost is incurred when a
       running stage is aborted, and that the cost of running part of a stage in suboptimal conditions is more
       beneficial overall. Introduce flex point (5) "condition cost model".
    4. Configuration of stage durations **t1**, **t2** and **t3** has not been temporally constrained. Introduce
       flex point (6) "dynamic stage durations".
    5. A resource error may occur passively, without notifying its current user (if any), or it may be active,
       forcibly terminating usage. This transition will also need to be tested, even if instructions indicate
       that error states need not be handled (i.e., the handler can be trivbial). Introduce flex point (7)
       "resource driver"
    6. Resource state "busy" is not defined relative to stage consumption of a resource. Introduce assumption
       (2) "idle until action taken".
    7. Stage execution is named but not defined, leaving in question the duration and sequence of resource-
       consuming tasks within a stage. Introduce flex point (8) "stage task sequence".



## System Requirements

### Flex Points

1. Sensor Interpretation
    1. role: synthesize a singular global temperature at time t from potentially many differing sensor readings
2. Sensor Bridge:
    1. role: synthesize a consistent data source for sensor input, allowing for potentially diverse sensor designs
3. Sensor Availability
    1. role: expand the software's sensor interface to perceive a condition of sensor absence
4. Stage Concurrency
    1. role: separate stages from periods such that any stage can run for any duration
5. Condition Cost Model
    1. role: evaluate the cost/benefit of changing the duration of a running stage
        - cause 1: inclement conditions
        - cause 2: dynamic stage duration change
    2. scope:
        - evaluation may be stage-centric, accounting for local factors such as stage completion percentage
        - evaluation may be global, accounting for aggregate factors such as total active stages
        - hybrid evaluation should remain possible
6. Dynamic Stage Durations
    1. role: enable stage scheduling to respond punctually when stage durations change, without re-initialization
    2. caveat: subject to flex point (5)
7. Resource Driver
    1. role: adapt resource usage to multiple modes of operation (at least `passive` and  active`)
        - configurable per resource type
        - mockable
8. Stage Task Sequence
    1. role: partition a stage into a sequence of tasks, each potentially having distinct resource usage


### Assumptions

1. Idle until Conditions Improve
    1. role: establish as a logical response to inclement conditions, that all processing should stop
    2. caveats: subject to flex point (5)
2. Idle until Action Taken
    1. role: simplify stage coordination by allowing a resource to be held by a stage while still in idle state
