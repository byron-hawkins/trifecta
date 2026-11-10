/* author: Copilot */
using System;
using System.Linq;
using System.Text.RegularExpressions;
using trifecta.stage;
using trifecta.test.common;
using Xunit;

namespace trifecta {
namespace test {
namespace stage {

[Collection("ComponentLog tests")]
public sealed class DirectorTests : ComponentLogTestBase {
  [Fact]
  public void ConfigureLogsRolesAndConstructedScenes() {
    var configuration = new DirectorConfiguration { roles = [
      new RoleConfiguration {
        id = 17, quantity = 2, tmin = 15, pmax = 80,
        scenes = [new SceneConfiguration { duration = TimeSpan.FromSeconds(3),
                                           resources = ["heater", "pump"] }]
      },
      new RoleConfiguration {
        id = 29, quantity = 1, tmin = 25, pmax = 90,
        scenes = [new SceneConfiguration { duration = TimeSpan.FromSeconds(5),
                                           resources = ["fan"] }]
      }
    ] };

    setup($"DirectorTests-{Guid.NewGuid():N}");
    try {
      Director.singleton.configure(configuration);
    } finally {
      finish();
    }

    var files = FindLogFiles();
    Assert.True(files.Length == 1,
                $"Expected one Director log file; found {files.Length}.");
    var lines = ReadLog().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Assert.Equal(
        configuration.roles.Sum(role => role.quantity),
        lines.Count(line => line.Contains("Constructing an Actor in role",
                                          StringComparison.OrdinalIgnoreCase)));
    var position = FindLine(lines, 0, "Director", "transition mode");
    Assert.True(position >= 0, "Missing Director configure log entry.");

    foreach (var role in configuration.roles) {
      for (var actor = 0; actor < role.quantity; actor++) {
        position = FindLine(lines, position + 1, "role", role.id.ToString(),
                            role.tmin.ToString(), role.pmax.ToString());
        Assert.True(
            position >= 0,
            $"Missing log entry for actor {actor + 1} of role {role.id}.");
        foreach (var scene in role.scenes) {
          var terms = new string[scene.resources.Count + 1];
          terms[0] = scene.duration.ToString();
          scene.resources.CopyTo(terms, 1);
          position = FindLine(lines, position + 1, terms);
          Assert.True(
              position >= 0,
              $"Missing scene log for actor {actor + 1} of role {role.id}.");
        }
      }
    }
  }

  private static int FindLine(string[] lines, int start,
                              params string[] terms) {
    for (var index = start; index < lines.Length; index++) {
      var matches = true;
      foreach (var term in terms) {
        if (!Regex.IsMatch(lines[index], $@"(?<!\w){Regex.Escape(term)}(?!\w)",
                           RegexOptions.IgnoreCase)) {
          matches = false;
          break;
        }
      }
      if (matches)
        return index;
    }
    return -1;
  }
}

}
}
}
