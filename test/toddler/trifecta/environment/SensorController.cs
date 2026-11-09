/* stub: Copilot */
using System.Collections.Generic;

namespace trifecta {
namespace environment {

public class SensorController : IConditionTypes {
  public static SensorController singleton =
      new(SensorInterpreter.singleton);

  public SensorController(SensorInterpreter interpreter) {
    _interpreter = interpreter;
  }

  public void Register(ISensorDriver driver) {}

  public void Release(ISensorDriver driver) {}

  public void Start() {}

  private readonly SensorInterpreter _interpreter;
  private readonly List<ISensorDriver> _actives = [];
  private readonly List<ISensorDriver> _passives = [];
}

}
}
