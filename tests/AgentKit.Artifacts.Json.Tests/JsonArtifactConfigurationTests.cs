// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Json.Tests;

/// <summary>Verifies the JSON target, settings, options, and instance-identity constraints.</summary>
public sealed class JsonArtifactConfigurationTests
{
    [Fact]
    public void InstanceId_WhenEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void InstanceId_WhenSupplied_FormatsAsAHyphenatedGuid()
    {
        var value = Guid.NewGuid();

        new JsonArtifactInstanceId(value).ToString().ShouldBe(value.ToString("D"));
    }

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var id = new JsonArtifactInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "agentkit-artifact-target");

        Should.Throw<ArgumentNullException>(() => new JsonArtifactTarget(null!, id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonArtifactTarget(" ", id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonArtifactTarget("relative/path", id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactTarget(path, default, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("expectedInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactTarget(path, id, (JsonStoreOpenMode) 9, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactTarget(path, id, JsonStoreOpenMode.OpenExisting, (JsonStoreRecoveryMode) 9)).ParamName.ShouldBe("recoveryMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactTarget(path, id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("recoveryMode");
    }

    [Fact]
    public void Target_WhenValid_NormalizesThePathAndPreservesTheModes()
    {
        var id = new JsonArtifactInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "agentkit-artifact-target", "..", "agentkit-artifact-target");

        var target = new JsonArtifactTarget(path, id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        target.DirectoryPath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedInstanceId.ShouldBe(id);
        target.OpenMode.ShouldBe(JsonStoreOpenMode.CreateIfMissing);
        target.RecoveryMode.ShouldBe(JsonStoreRecoveryMode.RecoverTornAppends);
    }

    [Fact]
    public void Settings_WhenABoundIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactSettings(0, 1, 1, 1, encoding)).ParamName.ShouldBe("maximumRecordBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactSettings(1, 0, 1, 1, encoding)).ParamName.ShouldBe("maximumDocumentBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactSettings(1, 1, 0, 1, encoding)).ParamName.ShouldBe("maximumPayloadBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonArtifactSettings(1, 1, 1, 0, encoding)).ParamName.ShouldBe("compactionRecordThreshold");
        Should.Throw<ArgumentNullException>(() => new JsonArtifactSettings(1, 1, 1, 1, null!)).ParamName.ShouldBe("encoding");
    }

    [Fact]
    public void Options_WhenDefaulted_ExposeTheDocumentedBounds()
    {
        var options = new JsonArtifactOptions();
        var settings = JsonArtifactSettings.CreateDefault();

        options.MaximumRecordBytes.ShouldBe(1_048_576);
        options.MaximumDocumentBytes.ShouldBe(1_048_576);
        options.MaximumPayloadBytes.ShouldBe(64 * 1_024 * 1_024);
        options.CompactionRecordThreshold.ShouldBe(4_096);
        settings.MaximumRecordBytes.ShouldBe(options.MaximumRecordBytes);
        settings.MaximumPayloadBytes.ShouldBe(options.MaximumPayloadBytes);
        settings.CompactionRecordThreshold.ShouldBe(options.CompactionRecordThreshold);
        settings.Encoding.ShouldBe(options.Encoding);
    }
}
