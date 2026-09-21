// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextContributorRegistration"/> boundary guards.</summary>
public sealed class ContextContributorRegistrationTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var registration = new ContextContributorRegistration(
            new ContextSourceKey("skills"),
            order: 3,
            ContextEvaluationFrequency.OncePerRun,
            required: true);

        registration.ContributorId.ShouldBe(new ContextSourceKey("skills"));
        registration.Order.ShouldBe(3);
        registration.Frequency.ShouldBe(ContextEvaluationFrequency.OncePerRun);
        registration.Required.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenContributorIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new ContextContributorRegistration(default, 0, ContextEvaluationFrequency.OncePerModelRequest, false));
    }
}
