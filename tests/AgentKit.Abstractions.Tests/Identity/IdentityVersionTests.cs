// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityVersion behavior and contracts.</summary>
public sealed class IdentityVersionTests: Conformance.LongIdentityConformanceTests<IdentityVersion>
{
    [Fact]
    public void IdentityVersion_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new IdentityVersion(-1));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void IdentityVersion_WhenValueIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new IdentityVersion(0));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override IdentityVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(IdentityVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
