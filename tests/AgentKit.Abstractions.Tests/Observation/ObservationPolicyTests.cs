// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationPolicy"/> validation.</summary>
public sealed class ObservationPolicyTests
{
    [Fact]
    public void Constructor_WhenBoundsAreNull_ThrowsArgumentNullExceptionNamingBounds()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ObservationPolicy(null!, []));

        exception.ParamName.ShouldBe("bounds");
    }

    [Fact]
    public void Constructor_WhenAllowedClassificationsAreNull_ThrowsArgumentExceptionNamingParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new ObservationPolicy(new ObservationBounds(8), null!));

        exception.ParamName.ShouldBe("allowedClassifications");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_ExposesBoundsAndClassifications()
    {
        var bounds = new ObservationBounds(8);
        var allowed = ImmutableHashSet.Create(DataClassification.Public, DataClassification.Internal);

        var policy = new ObservationPolicy(bounds, allowed);

        policy.Bounds.ShouldBeSameAs(bounds);
        policy.AllowedClassifications.ShouldBeSameAs(allowed);
    }
}
