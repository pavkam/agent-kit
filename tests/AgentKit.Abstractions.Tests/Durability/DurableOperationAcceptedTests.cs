// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableOperationAccepted"/> behavior and contracts.</summary>
public sealed class DurableOperationAcceptedTests
{
    [Fact]
    public void Constructor_WhenNameIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationAccepted(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            default,
            new DurableOperationVersion("v1"),
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationAccepted(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            new DurableOperationName("tool.call"),
            default,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationAccepted(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            new DurableOperationName("tool.call"),
            new DurableOperationVersion("v1"),
            default));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var accepted = Create();

        accepted.Binding.ShouldBe(DurabilityTestData.Binding());
        accepted.OccurredAt.ShouldBe(DurabilityTestData.Now);
        accepted.Name.ShouldBe(new DurableOperationName("tool.call"));
        accepted.Version.ShouldBe(new DurableOperationVersion("v1"));
        accepted.FencingToken.ShouldBe(DurabilityTestData.Token);
    }

    private static DurableOperationAccepted Create() =>
        new(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            new DurableOperationName("tool.call"),
            new DurableOperationVersion("v1"),
            DurabilityTestData.Token);
}
