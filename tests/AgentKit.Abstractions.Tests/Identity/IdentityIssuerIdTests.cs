// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityIssuerId behavior and contracts.</summary>
public sealed class IdentityIssuerIdTests: Conformance.StringIdentityConformanceTests<IdentityIssuerId>
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void IdentityIssuerId_WhenValueIsBlank_ThrowsArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityIssuerId(value!));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override IdentityIssuerId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(IdentityIssuerId subject) => subject.Value;
}
