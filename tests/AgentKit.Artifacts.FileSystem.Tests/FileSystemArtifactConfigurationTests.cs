// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.FileSystem.Tests;

/// <summary>Verifies the file-system target, settings, and options constraints.</summary>
public sealed class FileSystemArtifactConfigurationTests
{
    private static readonly FileSystemProfileKey _profile = new("artifacts");

    [Fact]
    public void Target_WhenConfigurationIsInvalid_ThrowsWithTheExactParameterName()
    {
        var root = Path.Combine(Path.GetTempPath(), "artifacts");

        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemArtifactTarget(default, new FileRootId("r"), root)).ParamName.ShouldBe("profileKey");
        Should.Throw<ArgumentException>(() => new FileSystemArtifactTarget(_profile, default, root)).ParamName.ShouldBe("rootId");
        Should.Throw<ArgumentNullException>(() => new FileSystemArtifactTarget(_profile, new FileRootId("r"), null!)).ParamName.ShouldBe("hostRootPath");
        Should.Throw<ArgumentException>(() => new FileSystemArtifactTarget(_profile, new FileRootId("r"), " ")).ParamName.ShouldBe("hostRootPath");
        Should.Throw<ArgumentException>(() => new FileSystemArtifactTarget(_profile, new FileRootId("r"), "relative/path")).ParamName.ShouldBe("hostRootPath");
    }

    [Fact]
    public void Target_WhenValid_NormalizesThePathAndPreservesEveryValue()
    {
        var root = Path.Combine(Path.GetTempPath(), "artifacts", "..", "artifacts");

        var target = new FileSystemArtifactTarget(_profile, new FileRootId("r"), root);

        target.ProfileKey.ShouldBe(_profile);
        target.RootId.ShouldBe(new FileRootId("r"));
        target.HostRootPath.ShouldBe(Path.GetFullPath(root));
    }

    [Fact]
    public void Settings_WhenABoundIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingIt()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemArtifactSettings(0, 1, 1, 1, TimeSpan.FromSeconds(1))).ParamName.ShouldBe("maximumRecordBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemArtifactSettings(1, 0, 1, 1, TimeSpan.FromSeconds(1))).ParamName.ShouldBe("maximumLogBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemArtifactSettings(1, 1, 0, 1, TimeSpan.FromSeconds(1))).ParamName.ShouldBe("maximumPayloadBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemArtifactSettings(1, 1, 1, 0, TimeSpan.FromSeconds(1))).ParamName.ShouldBe("compactionRecordThreshold");
        Should.Throw<ArgumentOutOfRangeException>(() => new FileSystemArtifactSettings(1, 1, 1, 1, TimeSpan.Zero)).ParamName.ShouldBe("effectAuthorizationLifetime");
    }

    [Fact]
    public void Options_WhenDefaulted_ExposeTheDocumentedBoundsAndMatchTheDefaultSettings()
    {
        var options = new FileSystemArtifactOptions();
        var settings = FileSystemArtifactSettings.CreateDefault();

        options.MaximumRecordBytes.ShouldBe(settings.MaximumRecordBytes);
        options.MaximumLogBytes.ShouldBe(settings.MaximumLogBytes);
        options.MaximumPayloadBytes.ShouldBe(settings.MaximumPayloadBytes);
        options.CompactionRecordThreshold.ShouldBe(settings.CompactionRecordThreshold);
        options.EffectAuthorizationLifetime.ShouldBe(settings.EffectAuthorizationLifetime);
    }
}
