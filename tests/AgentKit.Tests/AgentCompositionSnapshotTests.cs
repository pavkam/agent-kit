// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

/// <summary>Verifies AgentCompositionSnapshot behavior and contracts.</summary>
public sealed class AgentCompositionSnapshotTests
{
    [Fact]
    public void Constructor_WhenRunProfilesIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AgentCompositionSnapshot(null!, ComponentRegistrationSnapshot.Capture(new ServiceCollection())));

        exception.ParamName.ShouldBe("runProfiles");
    }

    [Fact]
    public void Constructor_WhenComponentRegistrationsIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new AgentCompositionSnapshot(new AgentRunProfilePublicationSnapshot([]), null!));

        exception.ParamName.ShouldBe("componentRegistrations");
    }

    [Fact]
    public void Constructor_WhenGivenValidEvidence_ExposesTheSameCapturedInstances()
    {
        var runProfiles = new AgentRunProfilePublicationSnapshot([]);
        var componentRegistrations = ComponentRegistrationSnapshot.Capture(new ServiceCollection());

        var snapshot = new AgentCompositionSnapshot(runProfiles, componentRegistrations);

        snapshot.RunProfiles.ShouldBeSameAs(runProfiles);
        snapshot.ComponentRegistrations.ShouldBeSameAs(componentRegistrations);
    }

    [Fact]
    public void With_WhenNoMemberIsChanged_ProducesAnEqualDistinctCopySharingTheSameEvidence()
    {
        var runProfiles = new AgentRunProfilePublicationSnapshot([]);
        var componentRegistrations = ComponentRegistrationSnapshot.Capture(new ServiceCollection());
        var original = new AgentCompositionSnapshot(runProfiles, componentRegistrations);

        var copy = original with { };

        copy.ShouldNotBeSameAs(original);
        copy.ShouldBe(original);
        copy.RunProfiles.ShouldBeSameAs(runProfiles);
        copy.ComponentRegistrations.ShouldBeSameAs(componentRegistrations);
    }
}
