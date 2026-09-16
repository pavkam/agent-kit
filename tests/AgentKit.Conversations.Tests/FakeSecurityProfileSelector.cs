// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conversations.Tests;

/// <summary>A deterministic, configurable <see cref="ISecurityProfileSelector"/> fake that captures internally consistent authorization for every request it is asked to authorize.</summary>
internal sealed class FakeSecurityProfileSelector: ISecurityProfileSelector
{
    /// <summary>Gets the number of times <see cref="SelectAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>Gets or sets the result <see cref="SelectAsync"/> returns; a matching captured authorization by default.</summary>
    public SecurityAuthorizationCaptureResult? Result { get; set; }

    /// <summary>
    /// Gets or sets a token source cancelled just before the cancellation check, so a test can simulate
    /// cancellation observed mid-call (after a lock is already held) rather than before the call ever starts.
    /// </summary>
    public CancellationTokenSource? CancelBeforeThrow { get; set; }

    public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(
        SecurityAuthorizationCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        CancelBeforeThrow?.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        if (Result is not null)
        {
            return ValueTask.FromResult(Result);
        }

        var authorization = TestSecurityEvidence.Authorization(
            request.Scope.AgentId,
            request.Scope.SessionId,
            request.Scope.Correlation,
            request.Identity);
        return ValueTask.FromResult<SecurityAuthorizationCaptureResult>(new SecurityAuthorizationCaptured(authorization));
    }
}
