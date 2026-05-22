namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Provides AES-256 encryption/decryption and SHA-256 hashing for PII fields.
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using AES-256-CBC.
    /// The returned byte array contains a random 16-byte IV prepended to the ciphertext.
    /// </summary>
    byte[] Encrypt(string plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> produced by <see cref="Encrypt"/>.
    /// Extracts the IV from the first 16 bytes and decrypts the remainder.
    /// </summary>
    string Decrypt(byte[] ciphertext);

    /// <summary>
    /// Computes the SHA-256 hex digest of the UTF-8 encoding of <paramref name="plaintext"/>.
    /// Returns a lowercase 64-character hex string.
    /// </summary>
    string ComputeHash(string plaintext);
}
