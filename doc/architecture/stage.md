*Author: Byron*

*Subject: outline a configuration model for actors and resources*

<!--- @context namespace trifecta.stage --->
`namespace trifecta.stage`

<!--- @context class ResourceType --->
## ResourceType

<!--- @type --->
- class ResourceType
  - composition
    - public ResourceName name
    - public int quantity
    - public TimeSpan tickrate
    - public ResourceMode mode

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
  - composition
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
- enum ResourceMode
  - composition
    - active
    - passive

<!--- @type --->
- enum TransitionMode
  - composition
    - quickstop
    - finishup
    - costmodel

