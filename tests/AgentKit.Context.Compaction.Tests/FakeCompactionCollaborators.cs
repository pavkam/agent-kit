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
        CompactionStrategyRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default) =>
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

/// <summary>A strategy that always declines, constructible by dependency injection so registrations can name it by type.</summary>
internal class DecliningCompactionStrategy: ICompactionStrategy
{
    public Task<CompactionStrategyResult> ProduceAsync(
        CompactionStrategyRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<CompactionStrategyResult>(new CompactionStrategyUnsupported(
            new CompactionRejection(CompactionRejectionKind.PolicyViolation, "declined by test strategy", ExtensionData.Empty)));
}

/// <summary>A second declining strategy type, so a registration can tell two implementations apart.</summary>
internal sealed class OtherDecliningCompactionStrategy: DecliningCompactionStrategy;

/// <summary>A summary generator that always fails, constructible by dependency injection.</summary>
internal class FailingSummaryGenerator: ICompactionSummaryGenerator
{
    public CompactionSummaryGeneratorDescriptor Descriptor { get; } = new(
        new CompactionSummaryGeneratorKey("test.generator"),
        new CompactionSummaryGeneratorVersion("1"),
        modelBacked: false,
        deterministic: true,
        maximumInputTokens: 1,
        maximumOutputTokens: 1);

    public Task<CompactionSummaryGenerationResult> GenerateAsync(
        CompactionSummaryRequest request,
        BudgetExecutionCapability? budget,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>A second generator type, so a registration can tell two implementations apart.</summary>
internal sealed class OtherFailingSummaryGenerator: FailingSummaryGenerator;

/// <summary>Collects every event a <see cref="RecordingCompactionEventSink"/> receives, shared through dependency injection.</summary>
internal sealed class CompactionEventLog
{
    public List<string> Entries { get; } = [];
}

/// <summary>An event sink that appends its type name to a shared log, optionally throwing first.</summary>
internal class RecordingCompactionEventSink(CompactionEventLog log): ICompactionEventSink
{
    public ValueTask PublishAsync(CompactionEvent compactionEvent, CancellationToken cancellationToken = default)
    {
        log.Entries.Add(GetType().Name);
        return ValueTask.CompletedTask;
    }
}

/// <summary>A second recording sink type.</summary>
internal sealed class OtherRecordingCompactionEventSink(CompactionEventLog log): RecordingCompactionEventSink(log);

/// <summary>A sink that always throws.</summary>
internal sealed class ThrowingCompactionEventSink: ICompactionEventSink
{
    public ValueTask PublishAsync(CompactionEvent compactionEvent, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("sink failed");
}

/// <summary>A compactor replacement that identifies itself.</summary>
internal sealed class ReplacementCompactor: ICompactor
{
    public Task<CompactionResult> CompactAsync(CompactionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<CompactionResult> CompactAsync(
        CompactionRequest request,
        SessionExecutionCapability session,
        BudgetExecutionCapability budget,
        HookDispatchContext? hooks,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>A cut selector replacement that identifies itself.</summary>
internal sealed class ReplacementCutSelector: ICompactionCutSelector
{
    public ValueTask<CompactionCutSelectionResult> SelectAsync(
        CompactionCutSelectionRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>A validator replacement that identifies itself.</summary>
internal sealed class ReplacementCompactionValidator: ICompactionValidator
{
    public ValueTask<CompactionValidationResult> ValidateAsync(
        CompactionValidationRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

/// <summary>A strategy resolver replacement that identifies itself.</summary>
internal sealed class ReplacementStrategyResolver: ICompactionStrategyResolver
{
    public ValueTask<CompactionStrategyResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionStrategyKey strategyKey,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>A summary-generator resolver replacement that identifies itself.</summary>
internal sealed class ReplacementSummaryGeneratorResolver: ICompactionSummaryGeneratorResolver
{
    public ValueTask<CompactionSummaryGeneratorResolution> ResolveAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionSummaryGeneratorKey generatorKey,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>An activation coordinator replacement that identifies itself.</summary>
internal sealed class ReplacementActivationCoordinator: ICompactionActivationCoordinator
{
    public Task<CompactionActivationResult> ActivateAsync(
        CompactionActivationRequest request,
        SessionExecutionCapability session,
        SecurityGrant activationGrant,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>An event dispatcher replacement that identifies itself.</summary>
internal sealed class ReplacementEventDispatcher: ICompactionEventDispatcher
{
    public ValueTask<CompactionEventDispatchResult> PublishAsync(
        ComponentKey<ICompactor> compactorKey,
        CompactionEvent compactionEvent,
        CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

/// <summary>A sink that observes cancellation by throwing, as a well-behaved asynchronous sink does.</summary>
internal sealed class CancellingCompactionEventSink: ICompactionEventSink
{
    public ValueTask PublishAsync(CompactionEvent compactionEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}
