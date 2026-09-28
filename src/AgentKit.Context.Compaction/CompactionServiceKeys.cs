// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Stable composite keys for keyed compaction DI registrations.</summary>
internal static class CompactionServiceKeys
{
    internal static string Strategy(ComponentKey<ICompactor> compactorKey, CompactionStrategyKey strategyKey) =>
        $"{compactorKey.Value}\0strategy\0{strategyKey.Value}";

    internal static string SummaryGenerator(ComponentKey<ICompactor> compactorKey, CompactionSummaryGeneratorKey generatorKey) =>
        $"{compactorKey.Value}\0generator\0{generatorKey.Value}";

    internal static string StrategyResolver(ComponentKey<ICompactor> compactorKey) =>
        $"{compactorKey.Value}\0strategy-resolver";

    internal static string SummaryGeneratorResolver(ComponentKey<ICompactor> compactorKey) =>
        $"{compactorKey.Value}\0summary-generator-resolver";

    internal static string EventDispatcher(ComponentKey<ICompactor> compactorKey) =>
        $"{compactorKey.Value}\0event-dispatcher";

    internal static string ActivationCoordinator(ComponentKey<ICompactor> compactorKey) =>
        $"{compactorKey.Value}\0activation";
}
