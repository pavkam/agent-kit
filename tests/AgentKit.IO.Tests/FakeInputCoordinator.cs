// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

/// <summary>An <see cref="IInputCoordinator"/> test double used only to exercise keyed registration and replacement.</summary>
internal sealed class FakeInputCoordinator: IInputCoordinator
{
    public ValueTask<InputAdmissionResult> AdmitAsync(InputAdmissionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<InputPromotionResult> PromoteAsync(InputPromotionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
