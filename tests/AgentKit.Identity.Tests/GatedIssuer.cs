// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

internal sealed class GatedIssuer([ServiceKey] IdentityIssuerId issuerId): IIdentityIssuer
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public IdentityIssuerDescriptor Descriptor { get; } = new(issuerId, new IdentityVersion(1));
    public ValueTask<IdentityValidationResult> ValidateEvidenceAsync(AuthenticationEvidence evidence, DateTimeOffset evaluatedAt, CancellationToken cancellationToken = default) => ValueTask.FromResult<IdentityValidationResult>(IdentityValidationPassed.Instance);
    public async ValueTask<IdentityNormalizationResult> NormalizeAsync(IdentityAssertion assertion, CancellationToken cancellationToken = default) { _ = Entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return null!; }
}
