using System.Security.Cryptography;

namespace TestingCore.Domain;

public static class Generators
{
    public static Guid Guid() => System.Guid.NewGuid();
    public static string Digits(int length)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        return string.Create(length, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        });
    }
    public static decimal Amount(decimal min, decimal max, int decimals = 2)
    {
        if (min > max || decimals is < 0 or > 4) throw new ArgumentOutOfRangeException();
        var scale = (decimal)Math.Pow(10, decimals);
        var lower = checked((int)Math.Ceiling(min * scale));
        var upper = checked((int)Math.Floor(max * scale));
        if (lower > upper || upper == int.MaxValue) throw new ArgumentOutOfRangeException();
        return RandomNumberGenerator.GetInt32(lower, upper + 1) / scale;
    }
}