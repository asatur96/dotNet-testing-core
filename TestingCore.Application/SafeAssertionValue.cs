using System.Text.Json;
using System.Text.RegularExpressions;

namespace TestingCore.Application;

internal static partial class SafeAssertionValue
{
    [GeneratedRegex(@"\d{6,}")]
    private static partial Regex LongDigits();

    [GeneratedRegex(@"password|secret|token|authorization|credential|private.?key|api.?key|cookie|connection.?string",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveName();

    public static bool IsSensitiveName(string name) => SensitiveName().IsMatch(name);

    public static string Format(object? value, bool sensitive = false)
    {
        if (sensitive) return "[redacted]";
        try
        {
            var element = JsonSerializer.SerializeToElement(value);
            var rendered = Render(element);
            return rendered.Length <= 2000 ? rendered : rendered[..2000] + "...";
        }
        catch (Exception error) when (error is JsonException or NotSupportedException)
        {
            return $"[{value?.GetType().Name ?? "null"}]";
        }
    }

    private static string Render(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(",", element.EnumerateObject().Select(property =>
            JsonSerializer.Serialize(property.Name) + ":" +
            (IsSensitiveName(property.Name) ? "\"[redacted]\"" : Render(property.Value)))) + "}",
        JsonValueKind.Array => "[" + string.Join(",", element.EnumerateArray().Select(Render)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(Mask(element.GetString() ?? "")),
        JsonValueKind.Number => Mask(element.GetRawText()),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => "null",
        _ => "[unavailable]"
    };

    private static string Mask(string value) => LongDigits().Replace(value, match =>
    {
        var digits = match.Value;
        return digits[..2] + new string('*', digits.Length - 4) + digits[^2..];
    });
}