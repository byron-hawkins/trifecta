*Author: Byron*

*Subject: outline a configuration model for loading and performing simulated events*

<!--- @context namespace trifecta.simulation --->
`namespace trifecta.simulation`

<!--- @context class ConfigurationFileLoader --->
## ConfigurationFileLoader

<!--- @type --->
- class ConfigurationFileLoader
  - operations
    - static T parse<T>(string filename)

<!--- @context class SensorDriverFactory --->
## SensorDriverFactory

<!--- @type --->
- class SensorDriverFactory
  - composition
    - [singleton]
    - string _rootpath
  - operations
    - SensorDriverFactory()
    - static ISensorDriver scripted(string configurationPath, string scriptPath)

<!--- @type --->
- class SensorConfiguration
  - composition
    - public int id
    - public string name
    - public TimeSpan tickinterval
    - public trifecta.environment.SensorType type
    - public trifecta.environment.SensorMode mode

<!--- @context class SensorScript --->
## SensorScript

<!--- @type --->
- class SensorScript<T>
  - composition
    - public List<Event> events

<!--- @type --->
- struct Event
  - composition
    - public TimeSpan duration
    - public T value

<!--- @context class ScriptedSensorDriver --->
## ScriptedSensorDriver

<!--- @type --->
- class ScriptedSensorDriver<T> : ISensorDriver
  - composition
    - protected SensorScript<T> _script
    - protected SensorConfiguration _configuration
  - operations
    - ScriptedSensorDriver(SensorScript<T> _script, SensorConfiguration _configuration)
    - void Start()
    - void Stop()

<!--- @context class ScriptedActiveSensorDriver --->
## ScriptedActiveSensorDriver

<!--- @type --->
- class ScriptedActiveSensorDriver<T> : ScriptedSensorDriver<T>
  - operations
    - ScriptedActiveSensorDriver(SensorScript<T> _script, SensorConfiguration _configuration)

<!--- @context class ScriptedPassiveSensorDriver --->
## ScriptedPassiveSensorDriver

<!--- @type --->
- class ScriptedPassiveSensorDriver<T> : ScriptedSensorDriver<T>
  - operations
    - ScriptedPassiveSensorDriver(SensorScript<T> _script, SensorConfiguration _configuration)
