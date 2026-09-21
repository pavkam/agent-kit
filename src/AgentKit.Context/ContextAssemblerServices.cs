// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>The keyed collaborators compiled for one <see cref="DefaultContextAssembler"/> instance.</summary>
/// <param name="History">The history pipeline selected for this assembler key.</param>
/// <param name="Instructions">The instruction resolver shared across assembler keys.</param>
/// <param name="Contributors">Contributors in deterministic evaluation order for the selected assembler key.</param>
/// <param name="Budgets">The budget allocator selected for the assembler key.</param>
/// <param name="Tools">The tool snapshot provider shared across assembler keys.</param>
/// <param name="Options">Validated context options shared across assembler keys.</param>
internal sealed record ContextAssemblerServices(
    IHistoryPipeline History,
    IInstructionResolver Instructions,
    IReadOnlyList<RegisteredContextContributor> Contributors,
    IContextBudgetAllocator Budgets,
    IToolSnapshotProvider Tools,
    AgentContextOptions Options);

/// <summary>One resolved contributor together with its registration metadata.</summary>
/// <param name="Registration">The stable registration captured at composition time.</param>
/// <param name="Contributor">The live contributor instance for the current scope.</param>
internal sealed record RegisteredContextContributor(
    ContextContributorRegistration Registration,
    IContextContributor Contributor);
