// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies ToolBatchEntry behavior and contracts.</summary>
public sealed class ToolBatchEntryTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var invocation = InvocationContext();
        var lease = InvokerLease();
        var prepared = PreparedCall(sourceOrdinal: 4);
        var accepted = AcceptedCall(sourceOrdinal: 4);
        var entry = new ToolBatchEntry(invocation, lease, prepared, accepted);
        entry.Invocation.ShouldBe(invocation);
        entry.InvokerLease.ShouldBeSameAs(lease);
        entry.Prepared.ShouldBe(prepared);
        entry.Accepted.ShouldBe(accepted);
        entry.ExecutionHints.ShouldBe(prepared.ExecutionPlan.Scheduling);
        entry.SourceOrdinal.ShouldBe(4);
    }

    [Fact]
    public void Constructor_WhenInvocationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(null!, InvokerLease(), PreparedCall(), AcceptedCall())).ParamName.ShouldBe("invocation");

    [Fact]
    public void Constructor_WhenInvokerLeaseIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(InvocationContext(), null!, PreparedCall(), AcceptedCall())).ParamName.ShouldBe("invokerLease");

    [Fact]
    public void Constructor_WhenPreparedIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(InvocationContext(), InvokerLease(), null!, AcceptedCall())).ParamName.ShouldBe("prepared");

    [Fact]
    public void Constructor_WhenAcceptedIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(InvocationContext(), InvokerLease(), PreparedCall(), null!)).ParamName.ShouldBe("accepted");

    [Fact]
    public void Constructor_WhenAcceptedNamesAnotherTool_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolBatchEntry(
            InvocationContext(), InvokerLease(), PreparedCall(), AcceptedCall(Descriptor(new ToolId("other"))))).ParamName.ShouldBe("accepted");

    [Fact]
    public void Constructor_WhenPreparedNamesAnotherToolVersion_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ToolBatchEntry(
            InvocationContext(), InvokerLease(), PreparedCall(Descriptor(version: new ToolVersion("2.0"))), AcceptedCall())).ParamName.ShouldBe("prepared");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = BatchEntry();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
