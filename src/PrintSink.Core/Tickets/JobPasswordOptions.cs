namespace PrintSink.Tickets;

/// <summary>
/// Carries an already-encrypted job password for the IPP operation attribute collection.
/// </summary>
public sealed class JobPasswordOptions
{
    private readonly byte[] encryptedPassword;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobPasswordOptions"/> class.
    /// </summary>
    /// <param name="encryptedPassword">The encrypted password bytes.</param>
    /// <param name="encryptionAlgorithm">The algorithm identifier to report in IPP attributes.</param>
    public JobPasswordOptions(ReadOnlySpan<byte> encryptedPassword, string encryptionAlgorithm)
    {
        ArgumentOutOfRangeException.ThrowIfZero(encryptedPassword.Length);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionAlgorithm);

        this.encryptedPassword = encryptedPassword.ToArray();
        EncryptionAlgorithm = encryptionAlgorithm;
    }

    /// <summary>
    /// Gets the encryption algorithm identifier.
    /// </summary>
    public string EncryptionAlgorithm { get; }

    /// <summary>
    /// Gets a copy of the encrypted password bytes.
    /// </summary>
    /// <returns>The encrypted password bytes.</returns>
    public byte[] GetEncryptedPassword()
    {
        return encryptedPassword.ToArray();
    }
}
