// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Messages.Content;

using System.Collections.Immutable;

/// <summary>Verifies captured projection policy, truthful omissions, and ordered structural provenance.</summary>
public sealed class ToolResultProjectionInfoTests
{
    [Fact]
    public void Constructor_WhenPolicyIsNull_RejectsExactParameter()
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentNullException>(() => new ToolResultProjectionInfo(null!, [], 0, 0));

        // Assert
        exception.ParamName.ShouldBe("policy");
    }

    [Fact]
    public void Constructor_WhenLossesAreUninitialized_RejectsExactParameter()
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentException>(() => new ToolResultProjectionInfo(Policy(), default, 0, 0));

        // Assert
        exception.ParamName.ShouldBe("losses");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_WhenOmittedBytesAreNegative_RejectsExactParameter(long omittedBytes)
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionInfo(Policy(), [ToolResultProjectionLoss.Truncated], omittedBytes, 0));

        // Assert
        exception.ParamName.ShouldBe("omittedBytes");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_WhenOmittedPartsAreNegative_RejectsExactParameter(int omittedParts)
    {
        // Arrange / Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionInfo(Policy(), [ToolResultProjectionLoss.Truncated], 0, omittedParts));

        // Assert
        exception.ParamName.ShouldBe("omittedParts");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    [InlineData(int.MaxValue)]
    public void Constructor_WhenLossIsUndefined_RejectsExactParameter(int rawLoss)
    {
        // Arrange
        ImmutableArray<ToolResultProjectionLoss> losses = [ToolResultProjectionLoss.Redacted, (ToolResultProjectionLoss) rawLoss];

        // Act
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionInfo(Policy(), losses, 1, 0));

        // Assert
        exception.ParamName.ShouldBe("losses");
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    public void Constructor_WhenOmissionsHaveNoContentLoss_RejectsLosslessAndStatusOnlyClaims(long bytes, int parts)
    {
        // Arrange
        ImmutableArray<ToolResultProjectionLoss> statusOnly = [ToolResultProjectionLoss.StatusCoarsened];

        // Act / Assert
        Should.Throw<ArgumentException>(() => new ToolResultProjectionInfo(Policy(), [], bytes, parts)).ParamName.ShouldBe("losses");
        Should.Throw<ArgumentException>(() => new ToolResultProjectionInfo(Policy(), statusOnly, bytes, parts)).ParamName.ShouldBe("losses");
    }

    [Theory]
    [InlineData(ToolResultProjectionLoss.Redacted)]
    [InlineData(ToolResultProjectionLoss.Normalized)]
    [InlineData(ToolResultProjectionLoss.Summarized)]
    [InlineData(ToolResultProjectionLoss.Truncated)]
    [InlineData(ToolResultProjectionLoss.Externalized)]
    public void Constructor_WhenContentLossIsRecorded_PreservesMeasuredOmissions(ToolResultProjectionLoss loss)
    {
        // Arrange
        var policy = Policy();

        // Act
        var info = new ToolResultProjectionInfo(policy, [ToolResultProjectionLoss.StatusCoarsened, loss], long.MaxValue, int.MaxValue);

        // Assert
        info.Policy.ShouldBeSameAs(policy);
        info.Losses.ShouldBe([ToolResultProjectionLoss.StatusCoarsened, loss]);
        info.OmittedBytes.ShouldBe(long.MaxValue);
        info.OmittedParts.ShouldBe(int.MaxValue);
    }

    [Theory]
    [InlineData(ToolResultProjectionLoss.Redacted)]
    [InlineData(ToolResultProjectionLoss.Normalized)]
    [InlineData(ToolResultProjectionLoss.Summarized)]
    [InlineData(ToolResultProjectionLoss.Truncated)]
    [InlineData(ToolResultProjectionLoss.Externalized)]
    [InlineData(ToolResultProjectionLoss.StatusCoarsened)]
    public void Constructor_WhenLossDoesNotOmitBytesOrParts_PreservesZeroCounts(ToolResultProjectionLoss loss)
    {
        // Arrange / Act
        var info = new ToolResultProjectionInfo(Policy(), [loss], 0, 0);

        // Assert
        info.Losses.ShouldBe([loss]);
        info.OmittedBytes.ShouldBe(0);
        info.OmittedParts.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenProjectionIsLossless_PreservesEmptyEvidence()
    {
        // Arrange / Act
        var info = new ToolResultProjectionInfo(Policy(), [], 0, 0);

        // Assert
        info.Losses.IsDefault.ShouldBeFalse();
        info.Losses.ShouldBeEmpty();
        info.OmittedBytes.ShouldBe(0);
        info.OmittedParts.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenLossesRepeat_PreservesOrderAndMultiplicity()
    {
        // Arrange
        ImmutableArray<ToolResultProjectionLoss> losses =
            [ToolResultProjectionLoss.Redacted, ToolResultProjectionLoss.Normalized, ToolResultProjectionLoss.Redacted];

        // Act
        var info = new ToolResultProjectionInfo(Policy(), losses, 3, 1);

        // Assert
        info.Losses.ShouldBe(losses);
    }

    [Fact]
    public void Equals_WhenEvidenceIsReconstructed_UsesStructuralEquality()
    {
        // Arrange
        var first = new ToolResultProjectionInfo(Policy(), [ToolResultProjectionLoss.Redacted, ToolResultProjectionLoss.Truncated], 42, 2);
        var second = new ToolResultProjectionInfo(Policy(), [ToolResultProjectionLoss.Redacted, ToolResultProjectionLoss.Truncated], 42, 2);

        // Act / Assert
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        (first with { }).ShouldBe(second);
        new HashSet<ToolResultProjectionInfo> { first, second }.Count.ShouldBe(1);
    }

    [Fact]
    public void Equals_WhenAnyEvidenceDiffers_DistinguishesProjections()
    {
        // Arrange
        ImmutableArray<ToolResultProjectionLoss> losses = [ToolResultProjectionLoss.Redacted, ToolResultProjectionLoss.Truncated];
        var original = new ToolResultProjectionInfo(Policy(), losses, 42, 2);
        ToolResultProjectionInfo[] changed =
        [
            new(Policy("other"), losses, 42, 2),
            new(Policy(version: 8), losses, 42, 2),
            new(Policy(), [ToolResultProjectionLoss.Truncated, ToolResultProjectionLoss.Redacted], 42, 2),
            new(Policy(), [ToolResultProjectionLoss.Redacted, ToolResultProjectionLoss.Truncated, ToolResultProjectionLoss.Redacted], 42, 2),
            new(Policy(), losses, 43, 2),
            new(Policy(), losses, 42, 3),
        ];

        // Act / Assert
        foreach (var projection in changed)
        {
            original.ShouldNotBe(projection);
        }

        original.Equals(null).ShouldBeFalse();
    }

    private static ToolResultProjectionPolicyReference Policy(string key = "history.tool-result", long version = 7) =>
        new(new ToolResultProjectionPolicyKey(key), new ToolResultProjectionPolicyVersion(version));
}
