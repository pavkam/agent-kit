// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Generates a bounded compaction summary from prepared segments.</summary>
public interface ICompactionSummaryGenerator
{
    /// <summary>Gets the descriptor for this generator implementation.</summary>
    public CompactionSummaryGeneratorDescriptor Descriptor { get; }

    /// <summary>Generates one summary for a bounded request.</summary>
    /// <param name="request">The summary request.</param>
    /// <param name="budget">The budget capability for model-backed generation.</param>
    /// <param name="cancellationToken">A token used to cancel generation.</param>
    /// <returns>A task that resolves to the closed generation outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public Task<CompactionSummaryGenerationResult> GenerateAsync(
        CompactionSummaryRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default);
}
