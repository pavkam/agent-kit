// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Output.Tests;

/// <summary>Verifies OutputProfileRegistration behavior and contracts.</summary>
public sealed class OutputProfileRegistrationTests
{
    [Fact]
    public void Constructor_WhenProcessorKeyIsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new OutputProfileRegistration(default));

        exception.ParamName.ShouldBe("processorKey");
    }

    [Fact]
    public void Constructor_WhenProcessorKeyIsValid_ExposesItExactly()
    {
        var key = new ComponentKey<IOutputProcessor>("profile");

        var registration = new OutputProfileRegistration(key);

        registration.ProcessorKey.ShouldBe(key);
    }

    [Fact]
    public void With_WhenNoMembersChanged_ProducesAnEqualClone()
    {
        var registration = new OutputProfileRegistration(new ComponentKey<IOutputProcessor>("profile"));

        var clone = registration with { };

        clone.ShouldBe(registration);
        clone.ShouldNotBeSameAs(registration);
    }
}
