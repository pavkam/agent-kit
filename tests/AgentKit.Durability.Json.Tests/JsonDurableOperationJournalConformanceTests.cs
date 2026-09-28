// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Runs the shared <see cref="IDurableOperationJournal"/> contract suite against the JSON adapter.</summary>
public sealed class JsonDurableOperationJournalConformanceTests
    : DurableOperationJournalConformanceTests<JsonDurableOperationJournalConformanceFixture>;
