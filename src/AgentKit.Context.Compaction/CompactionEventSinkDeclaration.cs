// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Captures one event sink registration for one compactor key.</summary>
internal sealed record CompactionEventSinkDeclaration(
    string CompactorKey,
    CompactionEventSinkRegistration Registration,
    Type SinkType);
