*Author: Byron*

*Subject: outline an event model for an autonomous stage executor*

`namespace trifecta.environment`

## SensorController

- class SensorController
  - contains
    - <singleton>
    - SensorInterpreter

## SensorInterpreter

- class SensorInterpreter
  - contains
    - <singleton>
  - performas
    - Temperature GetTemperature()
    - Pressure GetPressure()
    - void SetThresholds(IConditionAware.Temperature[])
    - void SetThresholds(IConditionAware.Pressure[])
    - void Register(IConditionAware)
    - void Paranoid(IConditionParanoid, Temperature t_granularity, Pressure p_granularity, TimeSpan normalization)


## Interfaces

- IConditionAware
  - types
    - using Temperature = floag
    - using Pressure = int
  - void ThresholdCross(Temperature)
  - void ThresholdCross(Pressure)

- IConditionParanoid : IConditionAware
  - void Change(Temperature)
  - void Change(Pressure)



