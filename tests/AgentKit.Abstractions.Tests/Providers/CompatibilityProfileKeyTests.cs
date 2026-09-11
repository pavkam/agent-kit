// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies CompatibilityProfileKey behavior and contracts.</summary>
public sealed class CompatibilityProfileKeyTests: Conformance.StringIdentityConformanceTests<CompatibilityProfileKey>
{
    [Fact]
    public void CompatibilityProfileKey_WhenOrdinalTextDiffers_PreservesDistinctIdentity()
    {
        var upper = new CompatibilityProfileKey("PROFILE");
        var lower = new CompatibilityProfileKey("profile");
        upper.ShouldNotBe(lower);
        upper.ToString().ShouldBe("PROFILE");
        default(CompatibilityProfileKey).ToString().ShouldBe(string.Empty);
    }

    /// <inheritdoc/>
    protected override CompatibilityProfileKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(CompatibilityProfileKey subject) => subject.Value;
}
