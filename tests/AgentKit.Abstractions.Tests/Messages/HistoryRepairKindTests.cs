// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages;

/// <summary>Verifies <see cref="HistoryRepairKind"/> values.</summary>
public sealed class HistoryRepairKindTests
{
    [Fact]
    public void HistoryRepairKind_WhenEnumerated_ContainsNormativeValues() =>
        Enum.GetValues<HistoryRepairKind>().ShouldBe([HistoryRepairKind.NormalizedContent, HistoryRepairKind.SettledInterruptedToolCall, HistoryRepairKind.ExcludedIncompleteAssistantContent, HistoryRepairKind.DegradedReasoning, HistoryRepairKind.OmittedReasoning, HistoryRepairKind.RelocatedMedia, HistoryRepairKind.RemovedProviderMetadata, HistoryRepairKind.MergedAdjacentUserContent, HistoryRepairKind.NormalizedToolCallIdentity, HistoryRepairKind.ProjectedImportedOrphanToolCall, HistoryRepairKind.ExcludedIncompleteMessage, HistoryRepairKind.ExcludedInstructionMessage]);
}
