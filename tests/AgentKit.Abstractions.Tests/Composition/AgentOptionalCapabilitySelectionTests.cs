// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies AgentOptionalCapabilitySelection behavior and contracts.</summary>
public sealed class AgentOptionalCapabilitySelectionTests
{
    [Fact]
    public void None_ExposesEveryFieldAbsentOrEmpty()
    {
        var none = AgentOptionalCapabilitySelection.None;
        none.ToolExecutor.ShouldBeNull();
        none.ArtifactCoordinator.ShouldBeNull();
        none.DurabilityProfile.ShouldBeNull();
        none.MemoryProfile.ShouldBeNull();
        none.GoalProfile.ShouldBeNull();
        none.Capabilities.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var capability = Capability();
        var selection = new AgentOptionalCapabilitySelection(
            new ComponentKey<IToolExecutor>("tools"),
            new ComponentKey<IArtifactCoordinator>("artifacts"),
            new DurabilityProfileKey("durability"),
            new MemoryProfileKey("memory"),
            new GoalProfileKey("goals"),
            [capability]);

        selection.ToolExecutor.ShouldBe(new ComponentKey<IToolExecutor>("tools"));
        selection.ArtifactCoordinator.ShouldBe(new ComponentKey<IArtifactCoordinator>("artifacts"));
        selection.DurabilityProfile.ShouldBe(new DurabilityProfileKey("durability"));
        selection.MemoryProfile.ShouldBe(new MemoryProfileKey("memory"));
        selection.GoalProfile.ShouldBe(new GoalProfileKey("goals"));
        selection.Capabilities.ShouldBe([capability]);
    }

    [Theory]
    [InlineData("toolExecutor")]
    [InlineData("artifactCoordinator")]
    [InlineData("durabilityProfile")]
    [InlineData("memoryProfile")]
    [InlineData("goalProfile")]
    public void Constructor_WhenAPresentOptionalKeyIsDefault_ThrowsExactArgumentOutOfRangeException(string parameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new AgentOptionalCapabilitySelection(
            parameter == "toolExecutor" ? default(ComponentKey<IToolExecutor>) : null,
            parameter == "artifactCoordinator" ? default(ComponentKey<IArtifactCoordinator>) : null,
            parameter == "durabilityProfile" ? default(DurabilityProfileKey) : null,
            parameter == "memoryProfile" ? default(MemoryProfileKey) : null,
            parameter == "goalProfile" ? default(GoalProfileKey) : null,
            []));

        exception.ParamName.ShouldBe(parameter);
    }

    [Fact]
    public void Constructor_WhenCapabilitiesIsDefault_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentOptionalCapabilitySelection(null, null, null, null, null, default))
            .ParamName.ShouldBe("capabilities");

    [Fact]
    public void Constructor_WhenCapabilitiesContainsNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentOptionalCapabilitySelection(null, null, null, null, null, [null!]))
            .ParamName.ShouldBe("capabilities");

    [Fact]
    public void Equality_WhenEveryFieldAndCapabilityOrderMatches_IsEqual()
    {
        var capability = Capability();
        var first = new AgentOptionalCapabilitySelection(new ComponentKey<IToolExecutor>("tools"), null, null, null, null, [capability]);
        var second = new AgentOptionalCapabilitySelection(new ComponentKey<IToolExecutor>("tools"), null, null, null, null, [capability]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenOtherIsNull_IsNotEqual() => AgentOptionalCapabilitySelection.None.Equals(null).ShouldBeFalse();

    [Fact]
    public void Equality_WhenAFieldDiffers_IsNotEqual() =>
        new AgentOptionalCapabilitySelection(new ComponentKey<IToolExecutor>("tools"), null, null, null, null, [])
            .ShouldNotBe(AgentOptionalCapabilitySelection.None);

    private static AgentCapabilityReference Capability() => new(new CapabilityId("cap"), new CapabilityProfileId("profile"));
}
