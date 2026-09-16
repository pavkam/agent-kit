// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies DurableOperationStart behavior and contracts.</summary>
public sealed class DurableOperationStartTests
{
    [Fact]
    public void DurableOperationStart_Constructor_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationStart(null!, DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("descriptor");
    }

    [Fact]
    public void DurableOperationStart_Constructor_WhenInitialStateIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new DurableOperationStart(DurabilityTestData.Descriptor(), null!, DurabilityTestData.Token, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("initialState");
    }

    [Fact]
    public void DurableOperationStart_Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), default, DurabilityTestData.Now));
        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void DurableOperationStart_Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var descriptor = DurabilityTestData.Descriptor();
        var payload = DurabilityTestData.Payload();
        var start = new DurableOperationStart(descriptor, payload, DurabilityTestData.Token, DurabilityTestData.Now);
        start.Descriptor.ShouldBe(descriptor);
        start.InitialState.ShouldBe(payload);
        start.FencingToken.ShouldBe(DurabilityTestData.Token);
        start.AcceptedAt.ShouldBe(DurabilityTestData.Now);
    }

    [Fact]
    public void With_WhenDescriptorIsNull_ThrowsArgumentNullException()
    {
        var start = new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        Should.Throw<ArgumentNullException>(() => _ = start with { Descriptor = null! }).ParamName.ShouldBe("Descriptor");
    }

    [Fact]
    public void With_WhenInitialStateIsNull_ThrowsArgumentNullException()
    {
        var start = new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        Should.Throw<ArgumentNullException>(() => _ = start with { InitialState = null! }).ParamName.ShouldBe("InitialState");
    }

    [Fact]
    public void With_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var start = new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        Should.Throw<ArgumentOutOfRangeException>(() => _ = start with { FencingToken = default }).ParamName.ShouldBe("FencingToken");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenDescriptorIsValid_UpdatesDescriptor()
    {
        var original = new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        var newDescriptor = DurabilityTestData.Descriptor();
        var updated = original with { Descriptor = newDescriptor };
        updated.Descriptor.ShouldBe(newDescriptor);
    }

    [Fact]
    public void With_WhenInitialStateIsValid_UpdatesInitialState()
    {
        var original = new DurableOperationStart(DurabilityTestData.Descriptor(), DurabilityTestData.Payload(), DurabilityTestData.Token, DurabilityTestData.Now);
        var newPayload = new OperationPayload(new SchemaVersion("v2"), [4, 5, 6]);
        var updated = original with { InitialState = newPayload };
        updated.InitialState.ShouldBe(newPayload);
    }
}
