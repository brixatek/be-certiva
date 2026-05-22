using Certiva.IdentityRegistry.Services;
using Microsoft.Extensions.Configuration;

namespace Certiva.Tests.Property.Helpers;

/// <summary>
/// Provides a pre-configured AesEncryptionService with a fixed 32-byte dev key for tests.
/// The key decodes to 32 zero bytes — safe for testing only.
/// </summary>
public static class TestEncryptionService
{
    // Base64 of 32 zero bytes — valid AES-256 test key
    private const string DevKey = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";

    public static AesEncryptionService Create()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = DevKey
            })
            .Build();

        return new AesEncryptionService(config);
    }
}
