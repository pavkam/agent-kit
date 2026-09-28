// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableReconciliationRequest"/> behavior and contracts.</summary>
public sealed class DurableReconciliationRequestTests
{
    [Fact]
    public void Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new DurableReconciliationRequest(null!, DurabilityTestData.Evidence()));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Constructor_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new DurableReconciliationRequest(DurabilityTestData.Descriptor(), null!));

        exception.ParamName.ShouldBe("evidence");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var descriptor = DurabilityTestData.Descriptor();
        var evidence = DurabilityTestData.Evidence();

        var request = new DurableReconciliationRequest(descriptor, evidence);

        request.Descriptor.ShouldBe(descriptor);
        request.Evidence.ShouldBe(evidence);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var request = new DurableReconciliationRequest(DurabilityTestData.Descriptor(), DurabilityTestData.Evidence());

        (request with { }).ShouldBe(request);
    }
}
