// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableOperationCheckpointed"/> behavior and contracts.</summary>
public sealed class DurableOperationCheckpointedTests
{
    [Fact]
    public void Constructor_WhenCheckpointIdIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationCheckpointed(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            default,
            DurableCheckpointKind.ToolCallRecorded,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("checkpointId");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationCheckpointed(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurabilityTestData.CheckpointId,
            (DurableCheckpointKind) 99,
            DurabilityTestData.Token));

        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenFencingTokenIsDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new DurableOperationCheckpointed(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurabilityTestData.CheckpointId,
            DurableCheckpointKind.ToolCallRecorded,
            default));

        exception.ParamName.ShouldBe("fencingToken");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsPropertiesWithoutCarryingState()
    {
        var checkpointed = Create();

        checkpointed.CheckpointId.ShouldBe(DurabilityTestData.CheckpointId);
        checkpointed.Kind.ShouldBe(DurableCheckpointKind.ToolCallRecorded);
        checkpointed.FencingToken.ShouldBe(DurabilityTestData.Token);
        checkpointed.GetType().GetProperties()
            .Select(static property => property.PropertyType)
            .ShouldNotContain(typeof(OperationPayload));
    }

    private static DurableOperationCheckpointed Create() =>
        new(
            DurabilityTestData.Binding(),
            DurabilityTestData.Now,
            DurabilityTestData.CheckpointId,
            DurableCheckpointKind.ToolCallRecorded,
            DurabilityTestData.Token);
}
