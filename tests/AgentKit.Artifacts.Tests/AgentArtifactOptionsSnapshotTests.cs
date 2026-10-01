// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="AgentArtifactOptionsSnapshot"/> validation and capture.</summary>
public sealed class AgentArtifactOptionsSnapshotTests
{
    [Fact]
    public void Create_WhenOptionsAreValid_CapturesEveryValue()
    {
        var options = new AgentArtifactOptions
        {
            MaximumArtifactBytes = 10,
            CopyBufferBytes = 2,
            PreparationLifetime = TimeSpan.FromMinutes(2),
            OrphanRetention = TimeSpan.FromHours(3),
            RequireDeclaredContentHash = false,
            ProcessOutputDirectory = new ArtifactDirectoryId("proc"),
            ProcessOutputRetentionPolicy = new ArtifactRetentionPolicyKey("run"),
            ProcessOutputClassification = DataClassification.Restricted,
        };

        var snapshot = AgentArtifactOptionsSnapshot.Create(options);
        options.MaximumArtifactBytes = 99;

        snapshot.MaximumArtifactBytes.ShouldBe(10);
        snapshot.CopyBufferBytes.ShouldBe(2);
        snapshot.PreparationLifetime.ShouldBe(TimeSpan.FromMinutes(2));
        snapshot.OrphanRetention.ShouldBe(TimeSpan.FromHours(3));
        snapshot.RequireDeclaredContentHash.ShouldBeFalse();
        snapshot.ProcessOutputDirectory.ShouldBe(new ArtifactDirectoryId("proc"));
        snapshot.ProcessOutputRetentionPolicy.ShouldBe(new ArtifactRetentionPolicyKey("run"));
        snapshot.ProcessOutputClassification.ShouldBe(DataClassification.Restricted);
    }

    [Fact]
    public void Create_WhenDefaultsAreUsed_AppliesTheDocumentedSafeMechanics()
    {
        var snapshot = AgentArtifactOptionsSnapshot.Create(new AgentArtifactOptions());

        snapshot.MaximumArtifactBytes.ShouldBe(16L * 1_024 * 1_024);
        snapshot.CopyBufferBytes.ShouldBe(64 * 1_024);
        snapshot.PreparationLifetime.ShouldBe(TimeSpan.FromHours(1));
        snapshot.OrphanRetention.ShouldBe(TimeSpan.FromHours(24));
        snapshot.RequireDeclaredContentHash.ShouldBeTrue();
        snapshot.ProcessOutputDirectory.ShouldBeNull();
        snapshot.ProcessOutputRetentionPolicy.ShouldBe(new ArtifactRetentionPolicyKey("session"));
        snapshot.ProcessOutputClassification.ShouldBe(DataClassification.Internal);
    }

    [Fact]
    public void Create_WhenOptionsAreNull_ThrowsNamingThem() =>
        Should.Throw<ArgumentNullException>(() => AgentArtifactOptionsSnapshot.Create(null!)).ParamName.ShouldBe("options");

    [Theory]
    [InlineData("MaximumArtifactBytes", typeof(ArgumentOutOfRangeException))]
    [InlineData("CopyBufferBytes", typeof(ArgumentOutOfRangeException))]
    [InlineData("PreparationLifetime", typeof(ArgumentOutOfRangeException))]
    [InlineData("OrphanRetention", typeof(ArgumentOutOfRangeException))]
    [InlineData("ProcessOutputClassification", typeof(ArgumentOutOfRangeException))]
    [InlineData("ProcessOutputRetentionPolicy", typeof(ArgumentException))]
    [InlineData("ProcessOutputDirectory", typeof(ArgumentException))]
    public void Create_WhenAValueIsInvalid_ThrowsNamingTheOption(string option, Type expected)
    {
        var options = new AgentArtifactOptions();
        switch (option)
        {
            case "MaximumArtifactBytes":
                options.MaximumArtifactBytes = 0;
                break;
            case "CopyBufferBytes":
                options.CopyBufferBytes = 0;
                break;
            case "PreparationLifetime":
                options.PreparationLifetime = TimeSpan.Zero;
                break;
            case "OrphanRetention":
                options.OrphanRetention = TimeSpan.FromSeconds(-1);
                break;
            case "ProcessOutputClassification":
                options.ProcessOutputClassification = (DataClassification) 99;
                break;
            case "ProcessOutputRetentionPolicy":
                options.ProcessOutputRetentionPolicy = default;
                break;
            default:
                options.ProcessOutputDirectory = default(ArtifactDirectoryId);
                break;
        }

        var exception = Record.Exception(() => AgentArtifactOptionsSnapshot.Create(options)).ShouldBeAssignableTo<ArgumentException>()!;

        exception.ShouldBeAssignableTo(expected);
        exception.ParamName.ShouldBe(option);
    }
}
