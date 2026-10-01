// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Json.Tests;

public sealed class JsonEvaluationStoreSettingsTests
{
    [Fact]
    public void Constructor_WhenArgumentsAreInvalid_ThrowsWithTheParameterName()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreSettings(0, 1, JsonEncodingSettings.CreateDefault())).ParamName.ShouldBe("maximumRecordBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonEvaluationStoreSettings(1, 0, JsonEncodingSettings.CreateDefault())).ParamName.ShouldBe("maximumDocumentBytes");
        Should.Throw<ArgumentNullException>(() => new JsonEvaluationStoreSettings(1, 1, null!)).ParamName.ShouldBe("encoding");
    }

    [Fact]
    public void CreateDefault_WhenCalled_UsesOneMebibyteBoundsAndTheCanonicalContract()
    {
        var settings = JsonEvaluationStoreSettings.CreateDefault();

        (settings.MaximumRecordBytes, settings.MaximumDocumentBytes).ShouldBe((1_048_576, 1_048_576));
        settings.Encoding.ShouldBe(JsonEncodingSettings.CreateDefault());
    }
}
