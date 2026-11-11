*author: Copilot*

# Simulation CLI

From the workspace root, run:

```sh
dotnet run --project simulator
```

Press Enter to stop. The CLI prints the actual timestamped log path at startup.
The default manifest is `configuration/simulation/manifest.json`. Its sensor
device and script files and stage files must be created before running.

To select another manifest, supply its path relative to the current directory
(or an absolute path):

```sh
dotnet run --project simulator -- path/to/manifest.json
```

Each manifest lists sensor device/script pairs and the resource and director
configuration files. These paths are relative to the top-level `configuration`
directory, not the manifest. Set `TRIFECTA_CONFIG_DIR` to use another
configuration root, or `TRIFECTA_LOG_DIR` to use another log root. Defaults are
`configuration/` and `log/` under the current directory.

The CLI starts the sensor controller, resource queue set, and director in that
order and stops them in reverse order. The underlying simulation components
are still under development.
