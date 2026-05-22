using Certiva.IdentityRegistry.Commands;
using Certiva.Infrastructure.Domain;

namespace Certiva.IdentityRegistry.Services;

/// <summary>
/// Core service for the Identity Registry module.
/// Handles Professional registration, Issuer onboarding, and related identity operations.
/// </summary>
public interface IIdentityRegistryService
{
    /// <summary>
    /// Registers a new Professional in the Identity Registry.
    /// </summary>
    /// <param name="cmd">The registration command containing all required fields.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Ok"/> with a <see cref="RegisterProfessionalResult"/> on success;
    /// <see cref="Result{T}.BadRequest"/> if validation fails;
    /// <see cref="Result{T}.Conflict"/> if a Professional with the same NationalId already exists for the tenant.
    /// </returns>
    Task<Result<RegisterProfessionalResult>> RegisterProfessionalAsync(
        RegisterProfessionalCommand cmd,
        CancellationToken ct);

    /// <summary>
    /// Onboards a new Issuer organization with VerificationStatus = Pending.
    /// Enforces case-insensitive OrganizationName uniqueness per tenant.
    /// </summary>
    /// <param name="cmd">The onboarding command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Ok"/> with an <see cref="OnboardIssuerResult"/> on success;
    /// <see cref="Result{T}.Conflict"/> (HTTP 409) if an Issuer with the same OrganizationName already exists for the tenant.
    /// </returns>
    Task<Result<OnboardIssuerResult>> OnboardIssuerAsync(OnboardIssuerCommand cmd, CancellationToken ct);

    /// <summary>
    /// Approves a pending Issuer, transitioning its VerificationStatus to Verified.
    /// Writes an AuditLog entry and publishes an IssuerApproved event via the outbox.
    /// </summary>
    /// <param name="cmd">The approval command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Ok"/> with the IssuerId on success;
    /// <see cref="Result{T}.NotFound"/> if the Issuer does not exist for the tenant;
    /// <see cref="Result{T}.Conflict"/> (HTTP 409) if the Issuer is already Verified.
    /// </returns>
    Task<Result<Guid>> ApproveIssuerAsync(ApproveIssuerCommand cmd, CancellationToken ct);

    /// <summary>
    /// Rejects a pending Issuer, transitioning its VerificationStatus to Rejected.
    /// Writes an AuditLog entry and publishes an IssuerRejected event via the outbox.
    /// </summary>
    /// <param name="cmd">The rejection command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="Result{T}.Ok"/> with the IssuerId on success;
    /// <see cref="Result{T}.NotFound"/> if the Issuer does not exist for the tenant;
    /// <see cref="Result{T}.Conflict"/> (HTTP 409) if the Issuer is already Rejected.
    /// </returns>
    Task<Result<Guid>> RejectIssuerAsync(RejectIssuerCommand cmd, CancellationToken ct);
}
