*Author: Byron*

*Subject: identify cases in both the customer experience and system behavioral capability*

## Elaborating the base criteria

**Context:** taken from the [exercise instructions](/doc/architecture/SRE_Evaluation_Ver.2.0.pdf) provided by Aydin

A number of key factors about behavioral capability remain in question about the system under development:

1. Sensors
    1. Although only a single measure each for temperature and pressure, at any given point in time, are mentioned
       in the stage map, plural is used in the parts list, e.g.: "temperature sensors". This suggests a system in
       which one logical temperature is concluded at time t on the basis of potentially many temperature sensors
       (and the same for pressure). To account for unknowns, introduce flex point "sensor interpretation".
    2. Interface details such as the communication mechanism and value representation are not specified. For
       example, it is not know whether a sensor (a) provides a driver for the software host, (b) performs push
       notification of changes, or (c) transforms hardware receptor signals into temprature units. It is also not
       known whether sensors can differ in these respects: can multiple sensors with differing operate simultaneously,
       each with a differing output mechanism? Introduce flex point "sensor bridge".
    3. Sensors may be added and replaced, but no states are designated for sensors. Suppose for example, factory
       conditions permit only replacement of all sensors together: there will be some period of sensor unavailability,
       which the software must respond to. Introduce flex point "sensor availability".
2. Stage Map
    1. ?? are stage groups mutually exclusive? conditions are not exclusive
    2. ?? what happens when conditions exceed the criteria of any stage?
    3. ?? when conditions cross a map threshold during execution of a stage, is it abandoned or completed?
        1. case 1: dangerous to continue where conditions do not permit
        2. case 2: dangerous to abandon a task partially completed
        3. case 3: no dangers are known: cost model to weigh [task incompletion] against
                   [partial execution in poor conditions] parameterized by [time remaining to completion],
                   [condition delta from optimal], [estimated resource wait time based on recent load]



## System Requirements

### Flex Points

1. Sensor Interpretation
    1. role: synthesize a singular global temperature at time t from potentially many differing sensor readings
2. Sensor Bridge:
    1. role: synthesize a consistent data source for sensor input, allowing for potentially diverse sensor designs
3. Sensor Availability
    1. role: expand the software's sensor interface to perceive a condition of sensor absence
