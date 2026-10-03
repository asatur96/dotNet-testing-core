using System.Collections;
using System.Text.Json;
using TestingCore.Domain;

namespace TestingCore.Application;

public enum ComparisonMode { Strict, Deep }

public sealed class GenericAssertions(TestContext context)
{
    public ValueAssertions<T> Value<T>(T actual) => new(this, actual);

    public GenericAssertions ShouldHaveValue<T>(
        T actual, T expected, ComparisonMode mode = ComparisonMode.Strict,
        string? label = null, bool sensitive = false)
    {
        var success = mode switch
        {
            ComparisonMode.Strict => EqualityComparer<T>.Default.Equals(actual, expected),
            ComparisonMode.Deep => DeepEqual(actual, expected),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        Record(label ?? "Value", SafeAssertionValue.Format(expected, sensitive),
            SafeAssertionValue.Format(actual, sensitive), success);
        return this;
    }

    public GenericAssertions ShouldContain(
        object? actual, object? expected, string? label = null, bool sensitive = false)
    {
        var actualElement = JsonSerializer.SerializeToElement(actual);
        var expectedElement = JsonSerializer.SerializeToElement(expected);
        var success = actualElement.ValueKind switch
        {
            JsonValueKind.String when expectedElement.ValueKind == JsonValueKind.String =>
                actualElement.GetString()!.Contains(expectedElement.GetString()!, StringComparison.Ordinal),
            JsonValueKind.Array => actualElement.EnumerateArray()
                .Any(item => EqualElements(item, expectedElement)),
            JsonValueKind.Object => ContainsElements(actualElement, expectedElement),
            _ => EqualElements(actualElement, expectedElement)
        };
        Record(label ?? "Contains", SafeAssertionValue.Format(expected, sensitive),
            SafeAssertionValue.Format(actual, sensitive), success);
        return this;
    }

    public GenericAssertions ShouldSatisfy<T>(
        T actual, Func<T, bool> predicate, string expectation,
        string? label = null, bool sensitive = false)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var success = predicate(actual);
        Record(label ?? "Predicate", expectation, SafeAssertionValue.Format(actual, sensitive), success);
        return this;
    }

    private void Record(string label, string expected, string actual, bool success)
    {
        context.AddArtifact(new ValidationArtifact(label, expected, actual, success, DateTimeOffset.UtcNow));
        if (!success)
            throw new InvalidOperationException($"{label}: expected {expected}, got {actual}");
    }

    private static bool DeepEqual(object? actual, object? expected) =>
        EqualElements(JsonSerializer.SerializeToElement(actual), JsonSerializer.SerializeToElement(expected));


    private static bool ContainsElements(JsonElement actual, JsonElement expected)
    {
        if (expected.ValueKind != JsonValueKind.Object || actual.ValueKind != JsonValueKind.Object)
            return EqualElements(actual, expected);
        foreach (var property in expected.EnumerateObject())
        {
            if (!actual.TryGetProperty(property.Name, out var value) ||
                !ContainsElements(value, property.Value))
                return false;
        }
        return true;
    }

    private static bool EqualElements(JsonElement actual, JsonElement expected)
    {
        if (actual.ValueKind != expected.ValueKind) return false;
        switch (actual.ValueKind)
        {
            case JsonValueKind.Object:
                var actualProperties = actual.EnumerateObject().ToArray();
                var expectedProperties = expected.EnumerateObject().ToArray();
                return actualProperties.Length == expectedProperties.Length &&
                    expectedProperties.All(property =>
                        actual.TryGetProperty(property.Name, out var value) &&
                        EqualElements(value, property.Value));
            case JsonValueKind.Array:
                var actualItems = actual.EnumerateArray().ToArray();
                var expectedItems = expected.EnumerateArray().ToArray();
                return actualItems.Length == expectedItems.Length &&
                    actualItems.Zip(expectedItems).All(pair => EqualElements(pair.First, pair.Second));
            case JsonValueKind.Number:
                return actual.TryGetDecimal(out var left) && expected.TryGetDecimal(out var right)
                    ? left == right : actual.GetRawText() == expected.GetRawText();
            case JsonValueKind.String:
                return actual.GetString() == expected.GetString();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;
            default:
                return actual.GetRawText() == expected.GetRawText();
        }
    }
}

public sealed class ValueAssertions<T>(GenericAssertions assertions, T actual)
{
    public ValueAssertions<T> ShouldHaveValue(
        T expected, ComparisonMode mode = ComparisonMode.Strict,
        string? label = null, bool sensitive = false)
    {
        assertions.ShouldHaveValue(actual, expected, mode, label, sensitive);
        return this;
    }

    public ValueAssertions<T> ShouldContain(object? expected, string? label = null, bool sensitive = false)
    {
        assertions.ShouldContain(actual, expected, label, sensitive);
        return this;
    }

    public ValueAssertions<T> ShouldSatisfy(
        Func<T, bool> predicate, string expectation,
        string? label = null, bool sensitive = false)
    {
        assertions.ShouldSatisfy(actual, predicate, expectation, label, sensitive);
        return this;
    }
}