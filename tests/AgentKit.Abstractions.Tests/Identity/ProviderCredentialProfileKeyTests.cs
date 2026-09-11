// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ProviderCredentialProfileKey behavior and contracts.</summary>
public sealed class ProviderCredentialProfileKeyTests: Conformance.StringIdentityConformanceTests<ProviderCredentialProfileKey>
{
    /// <inheritdoc/>
    protected override ProviderCredentialProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ProviderCredentialProfileKey subject) => subject.Value;
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ProviderCredentialProfileKey(null!)).ParamName.ShouldBe("value");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderCredentialProfileKey(value));
        _ = exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }
}
