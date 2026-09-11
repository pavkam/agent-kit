// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Supplies an authority identity for binding and value tests that must never evaluate security.</summary>
/// <remarks>Any authorization call fails immediately, exposing accidental evaluation during construction or selection.</remarks>
public sealed class UninvokedSecurityAuthority: ISecurityAuthority
{
    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Authorization was invoked by a test path that must only retain the authority.</exception>
    public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("This test authority must not be invoked.");
}
