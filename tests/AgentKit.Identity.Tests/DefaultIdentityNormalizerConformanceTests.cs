// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity.Tests;

/// <summary>Runs the reusable identity-normalizer contract against the first-party runtime.</summary>
public sealed class DefaultIdentityNormalizerConformanceTests: IdentityNormalizerConformanceTests<IdentityNormalizerConformanceFixture>
{
    /// <inheritdoc/>
    protected override IdentityNormalizerConformanceFixture CreateFixture() => new();
}
