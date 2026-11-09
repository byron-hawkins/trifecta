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

<!--- @context struct ResourceType --->
## ResourceType

<!--- @type --->
- struct ResourceType
  - composition
    - public ResourceName name
    - public int quantity

<!--- @context class ResourceSet --->
## ResourceSet

<!--- @type --->
- class ResourceSet
  - composition
    - public List<ResourceType> elements

<!--- @context class DirectorConfiguration --->
## DirectorConfiguration

<!--- @type --->
- class DirectorConfiguration
  - composition
    - public TransitionMode mode
    - public List<RoleConfiguration> roles

<!--- @context class RoleConfiguration --->
## RoleConfiguration

<!--- @type --->
- class RoleConfiguration
  - contains
    - public RoleId id
    - public Temperature tmin
    - public Pressure pmax
    - public List<SceneConfiguration> scenes

<!--- @context class SceneConfiguration --->
## SceneConfiguration

<!--- @type --->
- class SceneConfiguration
  - composition
    - public TimeSpan duration
    - public TimeSpan tickrate
    - public List<ResourceName> resources

## Enumerations

<!--- @type --->
- enum TransitionMode
  - composition
    - quickstop
    - finishup
    - costmodel

