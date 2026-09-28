// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableDispatchRequest"/> behavior and contracts.</summary>
public sealed class DurableDispatchRequestTests
{
    [Fact]
    public void Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new DurableDispatchRequest(null!, DurabilityTestData.Context()));

        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void Constructor_WhenExecutionContextIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new DurableDispatchRequest(DurabilityTestData.Descriptor(), null!));

        exception.ParamName.ShouldBe("executionContext");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var descriptor = DurabilityTestData.Descriptor();
        var context = DurabilityTestData.Context();

        var request = new DurableDispatchRequest(descriptor, context);

        request.Descriptor.ShouldBe(descriptor);
        request.ExecutionContext.ShouldBe(context);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var request = new DurableDispatchRequest(DurabilityTestData.Descriptor(), DurabilityTestData.Context());

        (request with { }).ShouldBe(request);
    }
}
