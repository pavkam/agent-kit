// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using System.Globalization;

using AgentKit;

/// <summary>Verifies ProviderEndpointProfileVersion behavior and contracts.</summary>
public sealed class ProviderEndpointProfileVersionTests: Conformance.LongIdentityConformanceTests<ProviderEndpointProfileVersion>
{
    /// <inheritdoc/>
    protected override ProviderEndpointProfileVersion Create(long value) => new(value);
    /// <inheritdoc/>
    protected override long GetValue(ProviderEndpointProfileVersion subject) => subject.Value;
    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProfileVersions_WhenNotPositive_ThrowArgumentOutOfRangeException(long value)
    {
        var endpoint = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderEndpointProfileVersion(value));
        endpoint.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ProfileVersions_WhenMaximum_RetainInvariantValues() => new ProviderEndpointProfileVersion(long.MaxValue).ToString().ShouldBe(long.MaxValue.ToString(CultureInfo.InvariantCulture));
}
