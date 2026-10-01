// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Captures one summary-generator registration for one compactor key so duplicates and profiles can be validated.</summary>
/// <param name="CompactorKey">The exact compactor key text the generator is registered under.</param>
/// <param name="Registration">The registration metadata supplied at registration time.</param>
/// <param name="ImplementationType">The concrete generator type, used to tell an idempotent repeat from a conflicting duplicate.</param>
internal sealed record CompactionSummaryGeneratorDeclaration(
    string CompactorKey,
    CompactionSummaryGeneratorRegistration Registration,
    Type ImplementationType);
