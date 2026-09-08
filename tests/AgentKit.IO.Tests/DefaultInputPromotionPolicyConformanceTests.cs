// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.IO.Tests;

using AgentKit.Conformance;

/// <summary>Runs portable promotion-policy conformance against the publicly composed first-party policy.</summary>
public sealed class DefaultInputPromotionPolicyConformanceTests:
    InputPromotionPolicyConformanceTests<DefaultInputPromotionPolicyConformanceFixture>
{
    /// <inheritdoc/>
    protected override DefaultInputPromotionPolicyConformanceFixture CreateFixture() => new();
}
