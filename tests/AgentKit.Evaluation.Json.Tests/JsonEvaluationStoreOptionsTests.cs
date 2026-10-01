// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class JsonEvaluationStoreOptionsTests
{
    [Fact]
    public void Constructor_WhenCreated_HasTheDocumentedDefaults()
    {
        var options = new JsonEvaluationStoreOptions();

        (options.MaximumRecordBytes, options.MaximumDocumentBytes).ShouldBe((1_048_576, 1_048_576));
        options.Encoding.ShouldBe(JsonEncodingSettings.CreateDefault());
    }
}
