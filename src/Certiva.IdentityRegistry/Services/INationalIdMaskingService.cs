namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Masks NationalId values for API responses, replacing all but the last four characters with asterisks.
/// </summary>
public interface INationalIdMaskingService
{
    /// <summary>
    /// Masks a NationalId string.
    /// If <paramref name="nationalId"/> is null or empty, returns an empty string.
    /// If <paramref name="nationalId"/> has 4 or fewer characters, returns all asterisks.
    /// Otherwise returns (length - 4) asterisks followed by the last 4 characters.
    /// </summary>
    string Mask(string nationalId);
}
