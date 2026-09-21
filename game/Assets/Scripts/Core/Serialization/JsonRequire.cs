// Pure C# -- no UnityEngine reference. See game/README.md.
using System.Collections.Generic;
using System.Text.Json;

namespace Thaivia.Core.Serialization;

/// <summary>
/// Strict field access over a parsed <see cref="JsonElement"/>. Every
/// method here either returns a value or throws
/// <see cref="MapPackFieldException"/> naming the exact dotted path -- it
/// never falls back to a CLR default the way naive attribute-based
/// deserialization would for a missing/mistyped field. This is what makes
/// "unknown-but-required field -> hard error" a real, exercised code path
/// rather than an accident of `System.Text.Json`'s default leniency.
/// </summary>
internal static class JsonRequire
{
    public static JsonElement Property(JsonElement obj, string name, string path)
    {
        if (obj.ValueKind != JsonValueKind.Object)
        {
            throw new MapPackFieldException(path, $"expected a JSON object to read '{name}' from.");
        }

        if (!obj.TryGetProperty(name, out var value))
        {
            throw new MapPackFieldException($"{path}.{name}", "required field is missing.");
        }

        return value;
    }

    public static JsonElement OptionalProperty(JsonElement obj, string name)
    {
        if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value))
        {
            return value;
        }

        return default;
    }

    public static string String(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new MapPackFieldException($"{path}.{name}", $"expected a JSON string, found {value.ValueKind}.");
        }

        return value.GetString()!;
    }

    /// <summary>String field that may legitimately be JSON null (matches
    /// the schema's `["string", "null"]` fields, e.g.
    /// `source_snapshot_timestamp`).</summary>
    public static string? NullableString(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Null => null,
            _ => throw new MapPackFieldException($"{path}.{name}", $"expected a JSON string or null, found {value.ValueKind}."),
        };
    }

    public static bool Bool(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new MapPackFieldException($"{path}.{name}", $"expected a JSON boolean, found {value.ValueKind}.");
        }

        return value.GetBoolean();
    }

    public static long Int64(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result))
        {
            throw new MapPackFieldException($"{path}.{name}", $"expected a JSON integer, found {value.ValueKind}.");
        }

        return result;
    }

    public static int Int32(JsonElement obj, string name, string path) => checked((int)Int64(obj, name, path));

    public static double Double(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new MapPackFieldException($"{path}.{name}", $"expected a JSON number, found {value.ValueKind}.");
        }

        return value.GetDouble();
    }

    public static JsonElement Object(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new MapPackFieldException($"{path}.{name}", $"expected a JSON object, found {value.ValueKind}.");
        }

        return value;
    }

    public static JsonElement Array(JsonElement obj, string name, string path)
    {
        var value = Property(obj, name, path);
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new MapPackFieldException($"{path}.{name}", $"expected a JSON array, found {value.ValueKind}.");
        }

        return value;
    }

    public static List<T> MapArray<T>(JsonElement arrayElement, string path, System.Func<JsonElement, string, int, T> map)
    {
        var result = new List<T>();
        var i = 0;
        foreach (var item in arrayElement.EnumerateArray())
        {
            result.Add(map(item, $"{path}[{i}]", i));
            i++;
        }

        return result;
    }
}
