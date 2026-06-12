using System.Security.Cryptography;
using System.Text;
using PrintSink.Core.Settings;

namespace PrintSink.Core.Tests.Settings;

/// <summary>
/// Tests IPP job password option handling.
/// </summary>
[TestClass]
public sealed class JobPasswordOptionsTests
{
    /// <summary>
    /// Verifies SHA-256 password options store the expected digest bytes.
    /// </summary>
    [TestMethod]
    public void FromPassword_hashes_sha2_256_password()
    {
        JobPasswordOptions options = JobPasswordOptions.FromPassword("secret", "sha2-256");
        byte[] expected = SHA256.HashData(Encoding.UTF8.GetBytes("secret"));

        CollectionAssert.AreEqual(expected, options.GetEncryptedPassword());
        Assert.AreEqual("sha2-256", options.EncryptionMethod);
    }

    /// <summary>
    /// Verifies the none encryption method stores UTF-8 password bytes.
    /// </summary>
    [TestMethod]
    public void FromPassword_keeps_utf8_bytes_for_none()
    {
        JobPasswordOptions options = JobPasswordOptions.FromPassword("secret", "none");

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("secret"), options.GetEncryptedPassword());
        Assert.AreEqual("none", options.EncryptionMethod);
    }

    /// <summary>
    /// Verifies unsupported encryption keywords are rejected.
    /// </summary>
    [TestMethod]
    public void Constructor_rejects_unknown_encryption_method()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new JobPasswordOptions(Convert.ToBase64String([1, 2, 3]), "md5"));
    }
}
