// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Json.Tests;

/// <summary>Verifies the JSON target, settings, options, and instance-identity constraints.</summary>
public sealed class JsonMemoryConfigurationTests
{
    [Fact]
    public void InstanceId_WhenEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemoryInstanceId(Guid.Empty)).ParamName.ShouldBe("value");

    [Fact]
    public void InstanceId_WhenSupplied_FormatsAsAHyphenatedGuid()
    {
        var value = Guid.NewGuid();

        new JsonMemoryInstanceId(value).ToString().ShouldBe(value.ToString("D"));
    }

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var id = new JsonMemoryInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "agentkit-memory-target");

        Should.Throw<ArgumentNullException>(() => new JsonMemoryTarget(null!, id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonMemoryTarget(" ", id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentException>(() => new JsonMemoryTarget("relative/path", id, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("directoryPath");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemoryTarget(path, default, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("expectedInstanceId");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemoryTarget(path, id, (JsonStoreOpenMode) 9, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("openMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemoryTarget(path, id, JsonStoreOpenMode.OpenExisting, (JsonStoreRecoveryMode) 9)).ParamName.ShouldBe("recoveryMode");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemoryTarget(path, id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact)).ParamName.ShouldBe("recoveryMode");
    }

    [Fact]
    public void Target_WhenValid_NormalizesThePathAndPreservesTheModes()
    {
        var id = new JsonMemoryInstanceId(Guid.NewGuid());
        var path = Path.Combine(Path.GetTempPath(), "agentkit-memory-target", "..", "agentkit-memory-target");

        var target = new JsonMemoryTarget(path, id, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        target.DirectoryPath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedInstanceId.ShouldBe(id);
        target.OpenMode.ShouldBe(JsonStoreOpenMode.CreateIfMissing);
        target.RecoveryMode.ShouldBe(JsonStoreRecoveryMode.RecoverTornAppends);
    }

    [Fact]
    public void Settings_WhenABoundIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        var encoding = JsonEncodingSettings.CreateDefault();

        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemorySettings(0, 1, 1, encoding)).ParamName.ShouldBe("maximumRecordBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemorySettings(1, 0, 1, encoding)).ParamName.ShouldBe("maximumDocumentBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new JsonMemorySettings(1, 1, 0, encoding)).ParamName.ShouldBe("compactionRecordThreshold");
        Should.Throw<ArgumentNullException>(() => new JsonMemorySettings(1, 1, 1, null!)).ParamName.ShouldBe("encoding");
    }

    [Fact]
    public void Options_WhenDefaulted_ExposeTheDocumentedBounds()
    {
        var options = new JsonMemoryOptions();
        var settings = JsonMemorySettings.CreateDefault();

        options.MaximumRecordBytes.ShouldBe(16_777_216);
        options.MaximumDocumentBytes.ShouldBe(1_048_576);
        options.CompactionRecordThreshold.ShouldBe(4_096);
        settings.MaximumRecordBytes.ShouldBe(options.MaximumRecordBytes);
        settings.CompactionRecordThreshold.ShouldBe(options.CompactionRecordThreshold);
    }
}
