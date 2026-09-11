// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;



/// <summary>Verifies SessionProfileVersion behavior and contracts.</summary>
public sealed class SessionProfileVersionTests: Conformance.LongIdentityConformanceTests<SessionProfileVersion>
{
    [Fact]
    public void SessionProfileVersion_WhenValueIsZero_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new SessionProfileVersion(0));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("value");
    }

    /// <inheritdoc/>
    protected override SessionProfileVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(SessionProfileVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
