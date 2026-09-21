// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Loop.Tests;

/// <summary>Captures matching fresh test authorization for each requested loop operation.</summary>
internal sealed class FakeSecurityProfileSelector: ISecurityProfileSelector
{
    /// <summary>
    /// Gets or sets an override consulted before the normal capture. Returning <see langword="null"/> falls
    /// through to a matching capture, letting a test fail only a specific operation (for example, the second
    /// turn's capture) while every other capture succeeds.
    /// </summary>
    public Func<SecurityAuthorizationCaptureRequest, SecurityAuthorizationCaptureResult?>? Override { get; set; }

    /// <summary>Gets every capture request received, in call order.</summary>
    public List<SecurityAuthorizationCaptureRequest> Requests { get; } = [];

    /// <inheritdoc/>
    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add(request);
        return ValueTask.FromResult(Override?.Invoke(request) ?? new SecurityAuthorizationCaptured(
            TestSecurityEvidence.Authorization(
                request.Scope.AgentId, request.Scope.SessionId, request.Scope.Correlation, request.Identity)));
    }
}
