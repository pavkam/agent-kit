// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

using AgentKit;

/// <summary>Verifies SessionEntryTypeId behavior and contracts.</summary>
public sealed class SessionEntryTypeIdTests: Conformance.StringIdentityConformanceTests<SessionEntryTypeId>
{
    [Fact]
    public void SessionEntryTypeId_Constructor_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new SessionEntryTypeId(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SessionEntryTypeId_Constructor_WhenValueIsBlank_ThrowsArgumentException(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionEntryTypeId(value));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void SessionEntryTypeId_DefaultValue_HasNoWireIdentityAndFormatsAsEmpty()
    {
        var typeId = default(SessionEntryTypeId);
        typeId.Value.ShouldBeNull();
        typeId.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void SessionEntryTypeId_Constructor_WhenValueIsValid_PreservesStableWireIdentity()
    {
        var typeId = new SessionEntryTypeId("agentkit.session.message");
        typeId.Value.ShouldBe("agentkit.session.message");
        typeId.ToString().ShouldBe(typeId.Value);
        typeId.ShouldBe(new SessionEntryTypeId("agentkit.session.message"));
    }

    /// <inheritdoc/>
    protected override SessionEntryTypeId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(SessionEntryTypeId subject) => subject.Value;
}
