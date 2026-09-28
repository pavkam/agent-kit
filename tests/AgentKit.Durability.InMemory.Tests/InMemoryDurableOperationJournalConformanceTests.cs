// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Runs the shared <see cref="IDurableOperationJournal"/> contract suite against the in-memory adapter.</summary>
public sealed class InMemoryDurableOperationJournalConformanceTests
    : DurableOperationJournalConformanceTests<InMemoryDurableOperationJournalConformanceFixture>;
