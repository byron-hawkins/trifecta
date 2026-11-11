/* author: Copilot */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using trifecta.environment;
using trifecta.io;
using trifecta.resource;
using trifecta.simulation;
using trifecta.stage;

namespace trifecta {
namespace cli {

public static class Program {
  public static int Main(string[] args) {
    if (args.Length > 1) {
      Console.Error.WriteLine(
          "Usage: dotnet run --project simulator -- [manifest.json]");
      return 2;
    }

    try {
      Run(args);
      return 0;
    } catch (Exception exception) {
      Console.Error.WriteLine($"Simulation failed: {exception}");
      return 1;
    }
  }

  private static void Run(string[] args) {
    var configurationRoot = Path.GetFullPath(
        Environment.GetEnvironmentVariable("TRIFECTA_CONFIG_DIR") ??
        Path.Combine(Environment.CurrentDirectory, "configuration"));
    var logRoot = Path.GetFullPath(
        Environment.GetEnvironmentVariable("TRIFECTA_LOG_DIR") ??
        Path.Combine(Environment.CurrentDirectory, "log"));
    Environment.SetEnvironmentVariable("TRIFECTA_CONFIG_DIR",
                                       configurationRoot);

    var manifestPath =
        args.Length == 1
            ? Path.GetFullPath(args[0])
            : Path.Combine(configurationRoot, "simulation", "manifest.json");
    using var log = new SimulationFileLog(logRoot);
    ComponentLog.singleton.Configure(log);
    Console.WriteLine($"Log: {log.Path}");

    var manifest =
        ConfigurationFileLoader.parse<SimulationManifest>(manifestPath);
    manifest.Validate(configurationRoot);

    var sensorStarted = false;
    var resourcesStarted = false;
    var directorStarted = false;
    var resources = ConfigurationFileLoader.parse<ResourceSet>(
        ResolvePath(configurationRoot, manifest.resources));
    var director = ConfigurationFileLoader.parse<DirectorConfiguration>(
        ResolvePath(configurationRoot, manifest.director));

    // the interpreter reports a crossing whenever a reading passes one of the
    // roles' environment bounds, so those bounds are the thresholds
    SensorInterpreter.singleton.SetThresholds(new SensorInterpreter.Thresholds {
      t = director.roles.Select(r => r.tmin).Distinct().ToArray(),
      p = director.roles.Select(r => r.pmax).Distinct().ToArray()
    });

    try {
      foreach (var sensor in manifest.sensors) {
        var driver = SensorDriverFactory.scripted(sensor.device, sensor.script);
        SensorController.singleton.Register(driver);
      }
      SensorController.singleton.Start();
      sensorStarted = true;

      ResourceQueueSet.singleton.configure(resources);
      ResourceQueueSet.singleton.Start();
      resourcesStarted = true;

      Director.singleton.configure(director);
      Director.singleton.Start();
      directorStarted = true;

      Console.WriteLine("Simulation running. Press Enter to stop.");
      if (Console.ReadLine() is null) {
        throw new EndOfStreamException(
            "Console input closed before Enter was pressed.");
      }
    } finally {
      try {
        if (directorStarted)
          Director.singleton.Stop(false);
      } finally {
        try {
          if (resourcesStarted)
            ResourceQueueSet.singleton.Stop();
        } finally {
          try {
            if (sensorStarted)
              SensorController.singleton.Stop();
          } finally {
            ComponentLog.singleton.Flush();
          }
        }
      }
    }
  }

  private static string ResolvePath(string root, string path) {
    if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) {
      throw new InvalidDataException(
          "Manifest paths must be relative to the configuration directory.");
    }
    var fullPath = Path.GetFullPath(path, root);
    var relativePath = Path.GetRelativePath(root, fullPath);
    if (relativePath == ".." ||
        relativePath.StartsWith(".." + Path.DirectorySeparatorChar,
                                StringComparison.Ordinal)) {
      throw new InvalidDataException(
          $"Manifest path escapes the configuration directory: {path}");
    }
    return fullPath;
  }

  public sealed class SimulationManifest {
    [JsonRequired]
    public List<SensorEntry> sensors = null!;
    [JsonRequired]
    public string resources = null!;
    [JsonRequired]
    public string director = null!;

    public void Validate(string root) {
      if (sensors is null || sensors.Count == 0) {
        throw new InvalidDataException(
            "The manifest must contain at least one sensor.");
      }
      foreach (var sensor in sensors) {
        if (sensor is null)
          throw new InvalidDataException("A sensor entry cannot be null.");
        ResolvePath(root, sensor.device);
        ResolvePath(root, sensor.script);
      }
      ResolvePath(root, resources);
      ResolvePath(root, director);
    }
  }

  public sealed class SensorEntry {
    [JsonRequired]
    public string device = null!;
    [JsonRequired]
    public string script = null!;
  }

  private sealed class SimulationFileLog : ITextLogSink, IDisposable {
    private readonly StreamWriter _writer;
    public string Path { get; }

    public SimulationFileLog(string logRoot) {
      var directory = System.IO.Path.Combine(logRoot, "simulation");
      Directory.CreateDirectory(directory);
      var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd.HH-mm-ss.fffffff",
                                               CultureInfo.InvariantCulture);
      Path = System.IO.Path.Combine(directory, $"simulation.{timestamp}.log");
      _writer = new StreamWriter(new FileStream(
          Path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
    }

    public void Log(string message) { _writer.WriteLine(message); }
    public void Flush() { _writer.Flush(); }
    public void Dispose() { _writer.Dispose(); }
  }
}

}
}
