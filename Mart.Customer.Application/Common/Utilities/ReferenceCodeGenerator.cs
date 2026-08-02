using System.Security.Cryptography;

namespace Mart.Customer.Application.Common.Utilities;

public static class ReferenceCodeGenerator
{
    public static string GenerateWithPrefix(string prefix, int randomDigits = 6)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (randomDigits is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(randomDigits));
        }

        var normalizedPrefix = new string(prefix
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();
        var minimum = randomDigits == 1 ? 0 : (int)Math.Pow(10, randomDigits - 1);
        var maximum = (int)Math.Pow(10, randomDigits);

        return $"{normalizedPrefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(minimum, maximum)}";
    }

    public static string Generate(
        string firstValue,
        string secondValue,
        int segmentLength = 2,
        int randomDigits = 4)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondValue);

        if (segmentLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentLength));
        }

        if (randomDigits is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(randomDigits));
        }

        var firstSegment = GetSegment(firstValue, segmentLength);
        var secondSegment = GetSegment(secondValue, segmentLength);
        var minimum = randomDigits == 1 ? 0 : (int)Math.Pow(10, randomDigits - 1);
        var maximum = (int)Math.Pow(10, randomDigits);
        var randomNumber = RandomNumberGenerator.GetInt32(minimum, maximum);

        return $"{firstSegment}{secondSegment}-{randomNumber}";
    }

    private static string GetSegment(string value, int length)
    {
        var normalized = new string(value
            .Where(char.IsLetterOrDigit)
            .Take(length)
            .ToArray())
            .ToUpperInvariant();

        return normalized.PadRight(length, 'X');
    }
}
