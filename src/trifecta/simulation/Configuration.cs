/* author: Byron */
/* author: Copilot */

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using trifecta.environment;

namespace trifecta {
namespace simulation {

public class ConfigurationFileLoader {
  public static T parse<T>(string path) {
    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<T>(json, ParserJsonOptions.instance) ?? throw new InvalidDataException($"No data in '{path}'.");
  }
}

static class ParserJsonOptions {
  public static JsonSerializerOptions instance = Create();

  public static JsonSerializerOptions Create() {
    var resolver = new DefaultJsonTypeInfoResolver();
    resolver.Modifiers.Add(ApplyMappings);

    return new JsonSerializerOptions { IncludeFields = true, TypeInfoResolver = resolver };
  }

  private static void ApplyMappings(JsonTypeInfo typeInfo) {
    if (typeInfo.Kind != JsonTypeInfoKind.Object) { return; }

    foreach (var property in typeInfo.Properties) {
      if (property.AttributeProvider is not MemberInfo member) { continue; }

      switch (member.Name) {
        case "duration" when property.PropertyType == typeof(TimeSpan):
          property.Name = "duration-ms";
          property.CustomConverter = new MillisecondsTimeSpanConverter();
          break;
        case "id" when property.PropertyType == typeof(int): property.Name = "id"; break;
        case "name" when property.PropertyType == typeof(string): property.Name = "name"; break;
        case "tickinterval" when property.PropertyType == typeof(TimeSpan):
          property.Name = "tick-interval";
          property.CustomConverter = new MillisecondsTimeSpanConverter();
          break;
        case "type" when property.PropertyType == typeof(SensorType):
          property.Name = "type";
          property.CustomConverter = new NamedEnumConverter<SensorType>();
          break;
        case "mode" when property.PropertyType == typeof(SensorMode):
          property.Name = "mode";
          property.CustomConverter = new NamedEnumConverter<SensorMode>();
          break;
      }
    }
  }

  private sealed class MillisecondsTimeSpanConverter : JsonConverter<TimeSpan> {
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out var milliseconds)) {
        throw new JsonException("Expected an integer number of milliseconds.");
      }

      try {
        return TimeSpan.FromMilliseconds(milliseconds);
      } catch (OverflowException exception) { throw new JsonException("Milliseconds are outside the TimeSpan range.", exception); }
    }

    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options) {
      if (value.Ticks % TimeSpan.TicksPerMillisecond != 0) { throw new JsonException("TimeSpan must contain a whole number of milliseconds."); }

      writer.WriteNumberValue(value.Ticks / TimeSpan.TicksPerMillisecond);
    }
  }

  private sealed class NamedEnumConverter<T> : JsonConverter<T>
      where T : struct, Enum {
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      if (reader.TokenType == JsonTokenType.String) {
        var value = reader.GetString();
        foreach (var name in Enum.GetNames<T>()) {
          if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) { return Enum.Parse<T>(name); }
        }
      }

      throw new JsonException($"Expected a named {typeof(T).Name} value.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
      var name = Enum.GetName(value);
      if (name is null) { throw new JsonException($"Invalid {typeof(T).Name} value: {value}."); }

      writer.WriteStringValue(name);
    }
  }
}

}
}
