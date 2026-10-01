// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

/// <summary>Verifies <see cref="ToolEventSinkRegistration"/> validation and equality.</summary>
public sealed class ToolEventSinkRegistrationTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var registration = new ToolEventSinkRegistration(new ComponentId("audit"), 7);

        registration.Id.ShouldBe(new ComponentId("audit"));
        registration.Order.ShouldBe(7);
    }

    [Fact]
    public void Constructor_WhenIdentityIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolEventSinkRegistration(default, 0)).ParamName.ShouldBe("id");

    [Fact]
    public void Equals_WhenValuesMatch_IsEqual() =>
        new ToolEventSinkRegistration(new ComponentId("a"), 1).ShouldBe(new ToolEventSinkRegistration(new ComponentId("a"), 1));
}
