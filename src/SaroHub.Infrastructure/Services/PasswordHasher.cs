// src/SaroHub.Infrastructure/Services/PasswordHasher.cs
using System.Security.Cryptography;
using System.Text;
using SaroHub.Core.Abstractions;

namespace SaroHub.Infrastructure.Services;

/// <summary>PBKDF2-SHA256 password hasher. Never stores plain text.</summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSize    = 16;
    private const int HashSize    = 32;
    private const int Iterations  = 100_000;
    private const char Separator  = ':';

    public string Hash(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) throw new ArgumentException("Password cannot be empty.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainText), salt,
            Iterations, HashAlgorithmName.SHA256, HashSize);

        return $"{Convert.ToBase64String(salt)}{Separator}{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string plainText, string storedHash)
    {
        if (string.IsNullOrEmpty(plainText) || string.IsNullOrEmpty(storedHash)) return false;

        var parts = storedHash.Split(Separator);
        if (parts.Length != 2) return false;

        byte[] salt, expected;
        try
        {
            salt     = Convert.FromBase64String(parts[0]);
            expected = Convert.FromBase64String(parts[1]);
        }
        catch { return false; }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainText), salt,
            Iterations, HashAlgorithmName.SHA256, HashSize);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
