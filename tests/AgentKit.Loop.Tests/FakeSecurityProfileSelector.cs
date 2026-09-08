// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Captures matching fresh test authorization for each requested loop operation.</summary>
internal sealed class FakeSecurityProfileSelector: ISecurityProfileSelector
{
    /// <inheritdoc/>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(
            TestSupport.TestSecurityEvidence.Authorization(
                request.Scope.AgentId, request.Scope.SessionId, request.Scope.Correlation, request.Identity)));
    }
}
