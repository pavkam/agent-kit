// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using System.Globalization;

using AgentKit;

/// <summary>Verifies DurabilityProfileVersion behavior and contracts.</summary>
public sealed class DurabilityProfileVersionTests: Conformance.LongIdentityConformanceTests<DurabilityProfileVersion>
{
    [Fact]
    public void DurabilityProfileVersion_Constructor_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurabilityProfileVersion(-1));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void DurabilityProfileVersion_Constructor_WhenValueIsZero_Succeeds() => new DurabilityProfileVersion(0).Value.ShouldBe(0);
    [Fact]
    public void DurabilityProfileVersion_ToString_ReturnsInvariantCultureText() => new DurabilityProfileVersion(12).ToString().ShouldBe(12.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    protected override DurabilityProfileVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(DurabilityProfileVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => false;
}
