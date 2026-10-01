// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationContentCapturePolicy"/> defaults.</summary>
public sealed class ObservationContentCapturePolicyTests
{
    [Fact]
    public void Constructor_WhenNoArgumentsAreSupplied_DisablesCapture() => new ObservationContentCapturePolicy().Enabled.ShouldBeFalse();

    [Fact]
    public void Constructor_WhenEnabledExplicitly_EnablesCapture() => new ObservationContentCapturePolicy(true).Enabled.ShouldBeTrue();
}
