using System.Security.Cryptography;
using Mart.Customer.Application.Abstractions.Auth;

namespace Mart.Customer.Infrastructure.Auth;

public sealed class Pbkdf2PasswordVerifier : IPasswordVerifier
{
    private const int PasswordHashIterations = 100_000;
    private const int PasswordHashSize = 32;

    public bool Verify(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(storedSalt))
        {
            return string.Equals(storedHash, password, StringComparison.Ordinal);
        }

        try
        {
            var computedHash = HashPassword(password, storedSalt);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(storedHash),
                Convert.FromBase64String(computedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            PasswordHashIterations,
            HashAlgorithmName.SHA256,
            PasswordHashSize);

        return Convert.ToBase64String(hash);
    }
}
