/* author: Copilot */

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using trifecta.environment;
using trifecta.simulation;

namespace trifecta {
namespace stage {

public class ResourceType {
  public ResourceName name = string.Empty;
  public int quantity;
  public TimeSpan tickrate;
  public ResourceMode mode;
}

public class ResourceSet {
  public List<ResourceType> elements = [];
}

public class DirectorConfiguration {
  public TransitionMode mode;
  public List<RoleConfiguration> roles = [];
}

public class RoleConfiguration {
  public RoleId id;
  public Temperature tmin;
  public Pressure pmax;
  public List<SceneConfiguration> scenes = [];
}

public class SceneConfiguration {
  public TimeSpan duration;
  public TimeSpan tickrate;
  public List<ResourceName> resources = [];
}

public enum ResourceMode {
  active,
  passive,
}

public enum TransitionMode {
  quickstop,
  finishup,
  costmodel,
}

}
}
