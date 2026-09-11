// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelUsageUpdated behavior and contracts.</summary>
public sealed class ModelUsageUpdatedTests
{
    [Fact]
    public void ModelUsageUpdated_Constructor_WhenUsageNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ModelUsageUpdated(new ModelRequestId(Guid.NewGuid()), 1, null!));
        exception.ParamName.ShouldBe("usage");
    }

    [Fact]
    public void ModelUsageUpdated_Equality_WhenSameValues_InstancesAreEqual()
    {
        var requestId = new ModelRequestId(Guid.NewGuid());
        var usage = new ModelUsage(ModelUsageReportState.Final, 1, 2, null, null, null, null, ExtensionData.Empty);
        new ModelUsageUpdated(requestId, 1, usage).ShouldBe(new ModelUsageUpdated(requestId, 1, usage));
    }

    [Fact]
    public void ModelUsageUpdated_Constructor_WhenUsageIsNotReported_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new ModelUsageUpdated(new ModelRequestId(Guid.NewGuid()), 1, ModelUsage.NotReported));
        exception.ParamName.ShouldBe("usage");
    }
}
