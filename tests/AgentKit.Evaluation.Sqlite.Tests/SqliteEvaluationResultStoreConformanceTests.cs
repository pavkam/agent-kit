// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

/// <summary>Runs the shared <see cref="IEvaluationResultStore"/> contract suite against the SQLite adapter.</summary>
public sealed class SqliteEvaluationResultStoreConformanceTests: EvaluationResultStoreConformanceTests<SqliteEvaluationResultStoreConformanceFixture>;
