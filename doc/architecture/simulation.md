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
    - int _id
    - string _name
    - TimeSpan _tickinterval
    - trifecta.environment.SensorType _type
    - trifecta.environment.SensorMode _mode
  - operations
    - void Load(string path)

<!--- @context class SensorScript --->
## SensorScript

<!--- @type --->
- class SensorScript<T>
  - composition
    - List<Event> _events
  - operations
    - void Load(string path)

<!--- @type --->
- struct Event
  - composition
    - TimeSpan duration
    - T value

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
