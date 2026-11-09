/* stub: Copilot */
using System;
using System.Collections.Generic;

namespace trifecta {
namespace stage {

public class RoleConfiguration {
  public RoleId id;
  public int quantity;
  public Temperature tmin;
  public Pressure pmax;
  public List<SceneConfiguration> scenes = [];
}

public class SceneConfiguration {
  public TimeSpan duration;
  public List<ResourceName> resources = [];
}

public class DirectorConfiguration {
  public List<RoleConfiguration> roles = [];
}

}
}
