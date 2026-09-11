// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ProviderCredentialSourceKey behavior and contracts.</summary>
public sealed class ProviderCredentialSourceKeyTests: Conformance.StringIdentityConformanceTests<ProviderCredentialSourceKey>
{
    /// <inheritdoc/>
    protected override ProviderCredentialSourceKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ProviderCredentialSourceKey subject) => subject.Value;
    [Fact]
    public void Constructor_WhenTextNull_ThrowsArgumentNullException() => Should.Throw<ArgumentNullException>(() => new ProviderCredentialSourceKey(null!)).ParamName.ShouldBe("value");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ProviderCredentialSourceKey(value));
        _ = exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }
}
