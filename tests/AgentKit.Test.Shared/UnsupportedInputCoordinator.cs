// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>An <see cref="IInputCoordinator"/> test double that throws <see cref="NotSupportedException"/> from every member.</summary>
/// <remarks>A placeholder collaborator for tests whose scripted loop never admits or promotes input.</remarks>
public sealed class UnsupportedInputCoordinator: IInputCoordinator
{
    /// <inheritdoc/>
    public ValueTask<InputAdmissionResult> AdmitAsync(InputAdmissionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support input admission.");

    /// <inheritdoc/>
    public ValueTask<InputPromotionResult> PromoteAsync(InputPromotionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This test double does not support input promotion.");
}
