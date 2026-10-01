// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Sqlite.Tests;

public sealed class SqliteEvaluationStoreOptionsTests
{
    [Fact]
    public void Constructor_WhenCreated_HasTheDocumentedDefaults()
    {
        var options = new SqliteEvaluationStoreOptions();

        (options.LockTimeout, options.MaximumRecordBytes).ShouldBe((TimeSpan.FromSeconds(5), 1_048_576));
    }
}
