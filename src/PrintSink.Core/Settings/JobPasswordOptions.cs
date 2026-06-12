using System.Security.Cryptography;
using System.Text;

namespace PrintSink.Core.Settings;

/// <summary>
/// Describes an IPP job password captured by foreground job UI.
/// </summary>
public sealed class JobPasswordOptions
{
    private static readonly HashSet<string> SupportedEncryptionMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "sha2-256",
        "none",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPasswordOptions"/> class.
    /// </summary>
    /// <param name="encryptedPasswordBase64">The IPP-ready password bytes encoded as base64.</param>
    /// <param name="encryptionMethod">The IPP password encryption method keyword.</param>
    public JobPasswordOptions(string encryptedPasswordBase64, string encryptionMethod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptedPasswordBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionMethod);

        byte[] encryptedPassword = Convert.FromBase64String(encryptedPasswordBase64);
        if (encryptedPassword.Length == 0)
        {
            throw new ArgumentException("Password bytes are required.", nameof(encryptedPasswordBase64));
        }

        if (!SupportedEncryptionMethods.Contains(encryptionMethod))
        {
            throw new ArgumentOutOfRangeException(nameof(encryptionMethod), encryptionMethod, "Unsupported job password encryption method.");
        }

        EncryptedPasswordBase64 = encryptedPasswordBase64;
        EncryptionMethod = encryptionMethod;
    }

    /// <summary>
    /// Gets the IPP-ready password bytes encoded as base64.
    /// </summary>
    public string EncryptedPasswordBase64 { get; }

    /// <summary>
    /// Gets the IPP password encryption method keyword.
    /// </summary>
    public string EncryptionMethod { get; }

    /// <summary>
    /// Creates job password options from a foreground UI password.
    /// </summary>
    /// <param name="password">The user-entered password.</param>
    /// <param name="encryptionMethod">The IPP password encryption method keyword.</param>
    /// <returns>The transformed job password options.</returns>
    public static JobPasswordOptions FromPassword(string password, string encryptionMethod)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionMethod);

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] encryptedPassword = string.Equals(encryptionMethod, "sha2-256", StringComparison.OrdinalIgnoreCase)
            ? SHA256.HashData(passwordBytes)
            : passwordBytes;

        return new JobPasswordOptions(Convert.ToBase64String(encryptedPassword), encryptionMethod);
    }

    /// <summary>
    /// Gets a new copy of the IPP-ready password bytes.
    /// </summary>
    /// <returns>The IPP-ready password bytes.</returns>
    public byte[] GetEncryptedPassword()
    {
        return Convert.FromBase64String(EncryptedPasswordBase64);
    }
}
