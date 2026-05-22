namespace Certiva.Infrastructure.Domain;

/// <summary>
/// Strongly-typed identifier for a Professional entity.
/// </summary>
public readonly record struct ProfessionalId(Guid Value)
{
    public static ProfessionalId New() => new(Guid.NewGuid());
    public static ProfessionalId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string value, out ProfessionalId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new ProfessionalId(guid);
            return true;
        }
        result = default;
        return false;
    }
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed identifier for an Issuer entity.
/// </summary>
public readonly record struct IssuerId(Guid Value)
{
    public static IssuerId New() => new(Guid.NewGuid());
    public static IssuerId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string value, out IssuerId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new IssuerId(guid);
            return true;
        }
        result = default;
        return false;
    }
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed identifier for a Certificate entity.
/// </summary>
public readonly record struct CertificateId(Guid Value)
{
    public static CertificateId New() => new(Guid.NewGuid());
    public static CertificateId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string value, out CertificateId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new CertificateId(guid);
            return true;
        }
        result = default;
        return false;
    }
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed identifier for a CertificateTemplate entity.
/// </summary>
public readonly record struct TemplateId(Guid Value)
{
    public static TemplateId New() => new(Guid.NewGuid());
    public static TemplateId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string value, out TemplateId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new TemplateId(guid);
            return true;
        }
        result = default;
        return false;
    }
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed identifier for a Tenant (organization) entity.
/// </summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string value, out TenantId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new TenantId(guid);
            return true;
        }
        result = default;
        return false;
    }
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed identifier for a BulkIssueJob entity.
/// </summary>
public readonly record struct BulkJobId(Guid Value)
{
    public static BulkJobId New() => new(Guid.NewGuid());
    public static BulkJobId Parse(string value) => new(Guid.Parse(value));
    public static bool TryParse(string value, out BulkJobId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new BulkJobId(guid);
            return true;
        }
        result = default;
        return false;
    }
    public override string ToString() => Value.ToString();
}

/// <summary>
/// Strongly-typed identifier for an actor (user or system process).
/// Wraps a string rather than a Guid because actors can be external user IDs
/// (e.g. JWT sub claims) or well-known system identifiers (e.g. "system", "scheduler").
/// </summary>
public readonly record struct ActorId(string Value)
{
    /// <summary>
    /// Creates an ActorId from a user identifier string (e.g. a JWT subject claim).
    /// </summary>
    public static ActorId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new ActorId(value);
    }

    /// <summary>
    /// A well-known ActorId representing an automated system process.
    /// </summary>
    public static readonly ActorId System = new("system");

    public override string ToString() => Value;
}
