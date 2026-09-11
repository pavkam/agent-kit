// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ProviderCredentialProfileVersion behavior and contracts.</summary>
public sealed class ProviderCredentialProfileVersionTests: Conformance.LongIdentityConformanceTests<ProviderCredentialProfileVersion>
{
    /// <inheritdoc/>
    protected override ProviderCredentialProfileVersion Create(long value) => new(value);
    /// <inheritdoc/>
    protected override long GetValue(ProviderCredentialProfileVersion subject) => subject.Value;
    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ProfileVersions_WhenNotPositive_ThrowArgumentOutOfRangeException(long value)
    {
        var credential = Should.Throw<ArgumentOutOfRangeException>(() => new ProviderCredentialProfileVersion(value));
        credential.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ProfileVersions_WhenMaximum_RetainInvariantValues() => new ProviderCredentialProfileVersion(long.MaxValue).Value.ShouldBe(long.MaxValue);
}
