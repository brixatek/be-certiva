namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Default implementation of <see cref="INationalIdMaskingService"/>.
/// Masks NationalId values so that only the last four characters are visible.
/// </summary>
public sealed class NationalIdMaskingService : INationalIdMaskingService
{
    /// <inheritdoc />
    public string Mask(string nationalId)
    {
        if (string.IsNullOrEmpty(nationalId))
            return string.Empty;

        if (nationalId.Length <= 4)
            return new string('*', nationalId.Length);

        return new string('*', nationalId.Length - 4) + nationalId[^4..];
    }
}
