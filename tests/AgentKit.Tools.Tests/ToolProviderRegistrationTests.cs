// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

public sealed class ToolProviderRegistrationTests
{
    [Fact]
    public void Constructor_WhenSourceDefault_RejectsExactParameter()
    {
        var error = Should.Throw<ArgumentOutOfRangeException>(() => new ToolProviderRegistration(default));
        error.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        error.ParamName.ShouldBe("sourceId");
    }

    [Fact]
    public void Constructor_WhenSourceValid_RetainsExactKey()
    {
        var source = new ToolSourceId("Source");
        var registration = new ToolProviderRegistration(source);
        registration.SourceId.ShouldBe(source);
        registration.ShouldBe(new ToolProviderRegistration(source));
        registration.ShouldNotBe(new ToolProviderRegistration(new ToolSourceId("source")));
    }

    [Fact]
    public void With_WhenCloningWithoutChanges_ProducesAnEqualIndependentInstance()
    {
        var registration = new ToolProviderRegistration(new ToolSourceId("source"));

        var clone = registration with { };

        clone.ShouldNotBeSameAs(registration);
        clone.ShouldBe(registration);
        clone.SourceId.ShouldBe(registration.SourceId);
    }
}
