/* author: Byron */
/* author: Copilot */

using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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

    var options = new JsonSerializerOptions { IncludeFields = true, TypeInfoResolver = resolver };
    options.Converters.Add(new MillisecondsTimeSpanConverter());
    options.Converters.Add(new NamedEnumConverterFactory());
    return options;
  }

  private static void ApplyMappings(JsonTypeInfo typeInfo) {
    if (typeInfo.Kind != JsonTypeInfoKind.Object) { return; }

    foreach (var property in typeInfo.Properties) {
      if (property.AttributeProvider is not MemberInfo member) { continue; }

      switch (member.Name) {
        case "duration" when property.PropertyType == typeof(TimeSpan):
          property.Name = "duration-ms";
          break;
        case "id" when property.PropertyType == typeof(int): property.Name = "id"; break;
        case "name" when property.PropertyType == typeof(string): property.Name = "name"; break;
        case "tickinterval" when property.PropertyType == typeof(TimeSpan):
          property.Name = "tick-interval";
          break;
        case "minwork" when property.PropertyType == typeof(TimeSpan):
          property.Name = "min-work";
          break;
        case "maxwork" when property.PropertyType == typeof(TimeSpan):
          property.Name = "max-work";
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

  private sealed class NamedEnumConverterFactory : JsonConverterFactory {
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) {
      var converterType = typeof(NamedEnumConverter<>).MakeGenericType(typeToConvert);
      return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
  }

  private sealed class NamedEnumConverter<T> : JsonConverter<T>
      where T : struct, Enum {
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      if (reader.TokenType == JsonTokenType.String) { return ParseName(reader.GetString()); }
      throw new JsonException($"Expected a named {typeof(T).Name} value.");
    }

    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
      return ParseName(reader.GetString());
    }

    private static T ParseName(string? value) {
      foreach (var name in Enum.GetNames<T>()) {
        if (string.Equals(name, value, StringComparison.OrdinalIgnoreCase)) { return Enum.Parse<T>(name); }
      }
      throw new JsonException($"Expected a named {typeof(T).Name} value.");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
      writer.WriteStringValue(GetName(value));
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options) {
      writer.WritePropertyName(GetName(value));
    }

    private static string GetName(T value) {
      return Enum.GetName(value) ?? throw new JsonException($"Invalid {typeof(T).Name} value: {value}.");
    }
  }
}

}
}
