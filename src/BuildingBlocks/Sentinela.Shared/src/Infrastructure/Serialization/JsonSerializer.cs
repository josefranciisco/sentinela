using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentinela.Shared.Infrastructure.Serialization;

public static class JsonSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize<T>(T value)
    {
        return System.Text.Json.JsonSerializer.Serialize(value, DefaultOptions);
    }

    public static T? Deserialize<T>(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, DefaultOptions);
    }

    public static JsonDocument SerializeToDocument<T>(T value)
    {
        var json = Serialize(value);
        return JsonDocument.Parse(json);
    }

    public static T? DeserializeFromElement<T>(JsonElement element)
    {
        var json = element.GetRawText();
        return Deserialize<T>(json);
    }
}
