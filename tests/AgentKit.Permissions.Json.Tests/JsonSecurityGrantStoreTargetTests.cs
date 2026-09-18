// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies JsonSecurityGrantStoreTarget bootstrap validation, normalization, and value semantics.</summary>
public sealed class JsonSecurityGrantStoreTargetTests
{
    /// <summary>Verifies every invalid bootstrap coordinate is rejected with its exact parameter name before any field is assigned.</summary>
    [Fact]
    public void Constructor_WhenCoordinateIsInvalid_ThrowsExactArgument()
    {
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var path = TestTemporaryDirectory.Create();

        var nullPath = Should.Throw<ArgumentNullException>(
            () => new JsonSecurityGrantStoreTarget(
                null!, instanceId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));
        var blankPath = Should.Throw<ArgumentException>(
            () => new JsonSecurityGrantStoreTarget(
                " ", instanceId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));
        var relativePath = Should.Throw<ArgumentException>(
            () => new JsonSecurityGrantStoreTarget(
                "relative/path", instanceId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));
        var emptyId = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSecurityGrantStoreTarget(
                path, default, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact));
        var openMode = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSecurityGrantStoreTarget(
                path, instanceId, (JsonStoreOpenMode) 99, JsonStoreRecoveryMode.ValidateExact));
        var recoveryMode = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSecurityGrantStoreTarget(
                path, instanceId, JsonStoreOpenMode.OpenExisting, (JsonStoreRecoveryMode) 99));
        var impossibleCombination = Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonSecurityGrantStoreTarget(
                path, instanceId, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact));

        nullPath.ParamName.ShouldBe("directoryPath");
        blankPath.GetType().ShouldBe(typeof(ArgumentException));
        blankPath.ParamName.ShouldBe("directoryPath");
        relativePath.GetType().ShouldBe(typeof(ArgumentException));
        relativePath.ParamName.ShouldBe("directoryPath");
        emptyId.ParamName.ShouldBe("expectedStoreInstanceId");
        openMode.ParamName.ShouldBe("openMode");
        recoveryMode.ParamName.ShouldBe("recoveryMode");
        impossibleCombination.ParamName.ShouldBe("recoveryMode");
    }

    /// <summary>Verifies a fully qualified but non-canonical path is normalized to its canonical full path.</summary>
    [Fact]
    public void Constructor_WhenPathIsNotCanonical_NormalizesToFullPath()
    {
        var root = TestTemporaryDirectory.Create();
        var nonCanonical = Path.Combine(root, "child", "..", "sibling");
        var expected = Path.GetFullPath(nonCanonical);

        var target = new JsonSecurityGrantStoreTarget(
            nonCanonical,
            new JsonSecurityGrantStoreInstanceId(Guid.NewGuid()),
            JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends);

        target.DirectoryPath.ShouldBe(expected);
        target.DirectoryPath.ShouldNotBe(nonCanonical);
    }

    /// <summary>Verifies every accepted open-mode and recovery-mode combination other than the rejected pair is retained exactly.</summary>
    [Theory]
    [InlineData(JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact)]
    [InlineData(JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.RecoverTornAppends)]
    [InlineData(JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends)]
    public void Constructor_WhenCombinationIsPermitted_RetainsExactCoordinates(
        JsonStoreOpenMode openMode, JsonStoreRecoveryMode recoveryMode)
    {
        var path = TestTemporaryDirectory.Create();
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());

        var target = new JsonSecurityGrantStoreTarget(path, instanceId, openMode, recoveryMode);

        target.DirectoryPath.ShouldBe(Path.GetFullPath(path));
        target.ExpectedStoreInstanceId.ShouldBe(instanceId);
        target.OpenMode.ShouldBe(openMode);
        target.RecoveryMode.ShouldBe(recoveryMode);
    }

    /// <summary>Verifies a non-destructive copy preserves every bootstrap coordinate and compares equal by value.</summary>
    [Fact]
    public void With_WhenCopiedWithoutChanges_RetainsEveryCoordinate()
    {
        var instanceId = new JsonSecurityGrantStoreInstanceId(Guid.NewGuid());
        var path = TestTemporaryDirectory.Create();
        var original = new JsonSecurityGrantStoreTarget(
            path, instanceId, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);

        var copy = original with { };

        copy.ShouldBe(original);
        copy.ShouldNotBeSameAs(original);
    }
}
