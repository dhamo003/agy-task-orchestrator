using System.Text.Json;
using System.Text.Json.Serialization;

namespace Runner.Logging;

public static class JsonLogFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Format(TaskLogEntry entry)
    {
        return JsonSerializer.Serialize(entry, Options);
    }
}
