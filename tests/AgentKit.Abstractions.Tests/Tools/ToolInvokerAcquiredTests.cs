// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolInvokerAcquiredTests
{
    [Fact]
    public void Constructor_WhenLeaseIsNull_RejectsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ToolInvokerAcquired(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("lease");
    }

    [Fact]
    public void Constructor_WhenLeaseIsWrapped_DoesNotReadMetadataOrDuplicateAcquisition()
    {
        var lease = new UnreadableToolInvokerLease();
        var result = new ToolInvokerAcquired(lease);
        var copy = result with { };

        result.Lease.ShouldBeSameAs(lease);
        copy.Lease.ShouldBeSameAs(lease);
        result.ShouldBe(copy);
        result.GetHashCode().ShouldBe(copy.GetHashCode());
    }

    [Fact]
    public void Equals_WhenLeaseInstancesDiffer_PreservesIndependentAcquisitions()
    {
        var first = new ToolInvokerAcquired(new UnreadableToolInvokerLease());
        var second = new ToolInvokerAcquired(new UnreadableToolInvokerLease());

        first.Equals(null).ShouldBeFalse();
        first.ShouldNotBe(second);
        new HashSet<ToolInvokerAcquired> { first, second }.Count.ShouldBe(2);
    }
}
