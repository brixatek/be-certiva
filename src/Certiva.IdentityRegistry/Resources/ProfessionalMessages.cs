using System.Resources;

namespace Certiva.IdentityRegistry.Resources;

internal static class ProfessionalMessages
{
    private static readonly ResourceManager Rm = new(
        "Certiva.IdentityRegistry.Resources.ProfessionalMessages",
        typeof(ProfessionalMessages).Assembly);

    internal static string NameRequired       => Rm.GetString(nameof(NameRequired))!;
    internal static string NameTooLong        => Rm.GetString(nameof(NameTooLong))!;
    internal static string NationalIdRequired => Rm.GetString(nameof(NationalIdRequired))!;
    internal static string NationalIdInvalid  => Rm.GetString(nameof(NationalIdInvalid))!;
    internal static string ContactRequired    => Rm.GetString(nameof(ContactRequired))!;
    internal static string PhoneInvalid       => Rm.GetString(nameof(PhoneInvalid))!;
    internal static string EmailInvalid       => Rm.GetString(nameof(EmailInvalid))!;

    internal static string AlreadyExists(Guid professionalId)
        => string.Format(Rm.GetString(nameof(AlreadyExists))!, professionalId);
}
