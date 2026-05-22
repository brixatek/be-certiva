using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// AES-256-CBC implementation of <see cref="IEncryptionService"/>.
/// Reads the base64-encoded 32-byte key from <c>Encryption:Key</c> in configuration.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var keyBase64 = configuration["Encryption:Key"]
            ?? throw new InvalidOperationException("Encryption:Key is not configured.");

        _key = Convert.FromBase64String(keyBase64);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Encryption:Key must decode to exactly 32 bytes (AES-256). Got {_key.Length} bytes.");
    }

    /// <inheritdoc />
    public byte[] Encrypt(string plaintext)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;
        aes.GenerateIV(); // random 16-byte IV

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV to ciphertext: [16 bytes IV][ciphertext]
        var result = new byte[aes.IV.Length + ciphertext.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertext, 0, result, aes.IV.Length, ciphertext.Length);
        return result;
    }

    /// <inheritdoc />
    public string Decrypt(byte[] ciphertext)
    {
        const int ivLength = 16;
        if (ciphertext.Length <= ivLength)
            throw new ArgumentException("Ciphertext is too short to contain an IV.", nameof(ciphertext));

        var iv = new byte[ivLength];
        var encryptedBytes = new byte[ciphertext.Length - ivLength];
        Buffer.BlockCopy(ciphertext, 0, iv, 0, ivLength);
        Buffer.BlockCopy(ciphertext, ivLength, encryptedBytes, 0, encryptedBytes.Length);

        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = _key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    /// <inheritdoc />
    public string ComputeHash(string plaintext)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
