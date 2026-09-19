// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookDispatchMetadataTests
{
    [Fact]
    public void Constructor_WhenPointIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookDispatchMetadata(
            default, new HookDispatchId(Guid.NewGuid()), HookKernelTestData.Correlation,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(1)));

        exception.ParamName.ShouldBe("point");
    }

    [Fact]
    public void Constructor_WhenDispatchIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookDispatchMetadata(
            HookKernelTestData.Point, default, HookKernelTestData.Correlation,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(1)));

        exception.ParamName.ShouldBe("dispatchId");
    }

    [Fact]
    public void Constructor_WhenCorrelationIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new HookDispatchMetadata(
            HookKernelTestData.Point, new HookDispatchId(Guid.NewGuid()), null!,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch + TimeSpan.FromSeconds(1)));

        exception.ParamName.ShouldBe("correlation");
    }

    [Fact]
    public void Constructor_WhenDeadlineIsNotAfterTimestamp_ThrowsArgumentOutOfRangeException()
    {
        var timestamp = DateTimeOffset.UnixEpoch;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new HookDispatchMetadata(
            HookKernelTestData.Point, new HookDispatchId(Guid.NewGuid()), HookKernelTestData.Correlation,
            timestamp, timestamp));

        exception.ParamName.ShouldBe("deadline");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsEveryProperty()
    {
        var dispatchId = new HookDispatchId(Guid.NewGuid());
        var timestamp = DateTimeOffset.UnixEpoch;
        var deadline = timestamp + TimeSpan.FromSeconds(5);

        var metadata = new HookDispatchMetadata(
            HookKernelTestData.Point, dispatchId, HookKernelTestData.Correlation, timestamp, deadline);

        metadata.Point.ShouldBe(HookKernelTestData.Point);
        metadata.DispatchId.ShouldBe(dispatchId);
        metadata.Correlation.ShouldBe(HookKernelTestData.Correlation);
        metadata.Timestamp.ShouldBe(timestamp);
        metadata.Deadline.ShouldBe(deadline);
    }
}
