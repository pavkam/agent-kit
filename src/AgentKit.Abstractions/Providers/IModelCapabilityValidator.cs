// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Compares a request's requirements with a chosen model's declared
/// capabilities before any provider I/O occurs.
/// </summary>
/// <remarks>
/// <para>
/// Validation is a pure decision. An implementation MUST NOT perform provider
/// I/O, resolve credentials, or mutate agent state.
/// </para>
/// <para>
/// Silent downgrade is forbidden. A validator either reports full support,
/// reports every adjustment it made, or reports the exact unsupported
/// capabilities. There is no outcome in which a request is quietly altered.
/// </para>
/// <para>
/// Implementations must be thread-safe and are normally registered as
/// singletons.
/// </para>
/// </remarks>
public interface IModelCapabilityValidator
{
    /// <summary>
    /// Validates one model against one set of requirements.
    /// </summary>
    /// <param name="model">The chosen model descriptor.</param>
    /// <param name="requirements">The behaviors the request needs.</param>
    /// <param name="downgradePolicy">
    /// Whether unsupported behavior may be adjusted away or must fail.
    /// </param>
    /// <param name="cancellationToken">A token that cancels validation.</param>
    /// <returns>
    /// <see cref="CapabilitiesSupported"/> when nothing is lost,
    /// <see cref="CapabilitiesDowngraded"/> with every declared adjustment, or
    /// <see cref="CapabilitiesUnsupported"/> with every unmet requirement.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="model"/> or <paramref name="requirements"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="downgradePolicy"/> is not a defined enumeration value.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was signalled.
    /// </exception>
    public ValueTask<CapabilityValidationResult> ValidateAsync(
        ModelDescriptor model,
        ModelRequirements requirements,
        CapabilityDowngradePolicy downgradePolicy,
        CancellationToken cancellationToken = default);
}
