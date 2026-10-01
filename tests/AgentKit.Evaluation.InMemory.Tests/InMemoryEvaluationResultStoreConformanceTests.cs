// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.InMemory.Tests;

/// <summary>Runs the shared <see cref="IEvaluationResultStore"/> contract suite against the in-memory adapter.</summary>
public sealed class InMemoryEvaluationResultStoreConformanceTests: EvaluationResultStoreConformanceTests<InMemoryEvaluationResultStoreConformanceFixture>;
