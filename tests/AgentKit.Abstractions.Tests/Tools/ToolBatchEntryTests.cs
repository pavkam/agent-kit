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
        var hints = ExecutionHints();
        var entry = new ToolBatchEntry(invocation, lease, hints, 4);
        entry.Invocation.ShouldBe(invocation);
        entry.InvokerLease.ShouldBeSameAs(lease);
        entry.ExecutionHints.ShouldBe(hints);
        entry.SourceOrdinal.ShouldBe(4);
    }

    [Fact]
    public void Constructor_WhenInvocationIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(null!, InvokerLease(), ExecutionHints(), 0)).ParamName.ShouldBe("invocation");

    [Fact]
    public void Constructor_WhenInvokerLeaseIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(InvocationContext(), null!, ExecutionHints(), 0)).ParamName.ShouldBe("invokerLease");

    [Fact]
    public void Constructor_WhenExecutionHintsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolBatchEntry(InvocationContext(), InvokerLease(), null!, 0)).ParamName.ShouldBe("executionHints");

    [Fact]
    public void Constructor_WhenSourceOrdinalIsNegative_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolBatchEntry(InvocationContext(), InvokerLease(), ExecutionHints(), -1)).ParamName.ShouldBe("sourceOrdinal");

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = BatchEntry();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
