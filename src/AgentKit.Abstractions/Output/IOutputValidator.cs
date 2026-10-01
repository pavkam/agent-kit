// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Validates an already schema-valid output candidate against additional, application-defined rules.</summary>
/// <remarks>
/// The structured-output architecture describes resolving validators through a
/// component-key-scoped <c>IOutputValidatorCatalog</c>; that catalog is not
/// implemented. Every <see cref="IOutputValidator"/> is instead registered
/// additively and exposes a
/// stable <see cref="Name"/> that an <see cref="OutputDefinition"/>
/// references by <see cref="OutputValidatorReference"/> to select it. A
/// validator must never perform a hidden side effect; it inspects the
/// candidate and reports issues or passes it unchanged.
/// </remarks>
public interface IOutputValidator
{
    /// <summary>Gets the stable name an <see cref="OutputDefinition"/> references to select this validator.</summary>
    public string Name { get; }

    /// <summary>Validates one candidate.</summary>
    /// <param name="request">The validation request.</param>
    /// <param name="cancellationToken">A token used to cancel validation.</param>
    /// <returns>A task producing the closed validation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<OutputValidationResult> ValidateAsync(
        OutputValidationRequest request, CancellationToken cancellationToken = default);
}
