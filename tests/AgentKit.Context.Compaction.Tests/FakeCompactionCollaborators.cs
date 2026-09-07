// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction.Tests;

/// <summary>A scripted <see cref="ICompactionCutSelector"/> test double.</summary>
internal sealed class FakeCompactionCutSelector: ICompactionCutSelector
{
    public Func<CompactionCutSelectionRequest, CompactionCutSelectionResult>? OnSelect { get; set; }

    public ValueTask<CompactionCutSelectionResult> SelectAsync(
        CompactionCutSelectionRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnSelect?.Invoke(request) ?? throw new InvalidOperationException("not configured"));
}

/// <summary>A scripted <see cref="ICompactionStrategy"/> test double.</summary>
internal sealed class FakeCompactionStrategy: ICompactionStrategy
{
    public Func<CompactionStrategyRequest, CompactionStrategyResult>? OnProduce { get; set; }

    public Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(OnProduce?.Invoke(request) ?? throw new InvalidOperationException("not configured"));
}

/// <summary>A scripted <see cref="ICompactionValidator"/> test double.</summary>
internal sealed class FakeCompactionValidator: ICompactionValidator
{
    public Func<CompactionValidationRequest, CompactionValidationResult>? OnValidate { get; set; }

    public ValueTask<CompactionValidationResult> ValidateAsync(
        CompactionValidationRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnValidate?.Invoke(request) ?? throw new InvalidOperationException("not configured"));
}
