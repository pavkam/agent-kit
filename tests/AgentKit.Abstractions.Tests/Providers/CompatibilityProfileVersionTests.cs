// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies CompatibilityProfileVersion behavior and contracts.</summary>
public sealed class CompatibilityProfileVersionTests: Conformance.LongIdentityConformanceTests<CompatibilityProfileVersion>
{
    [Fact]
    public void CompatibilityProfileVersion_WhenMaximumValueProvided_PreservesValue()
    {
        var version = new CompatibilityProfileVersion(long.MaxValue);
        version.Value.ShouldBe(long.MaxValue);
        version.ToString().ShouldBe(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    protected override CompatibilityProfileVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(CompatibilityProfileVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
