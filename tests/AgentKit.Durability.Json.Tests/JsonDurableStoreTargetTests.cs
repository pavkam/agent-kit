// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Json.Tests;

/// <summary>Verifies the bootstrap target validates its root, identity, and effect selections without touching disk.</summary>
public sealed class JsonDurableStoreTargetTests
{
    private static readonly JsonDurableStoreInstanceId Identity = new(Guid.Parse(
        "3c9d40ab-1a2f-4c6d-8e4b-7a2f3d5e6b8c"));

    /// <summary>Verifies a null root is refused, because the journal fabricates no persistence target.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsNull_ThrowsForTheDirectoryPathArgument()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new JsonDurableStoreTarget(
            null!, Identity, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends));

        exception.ParamName.ShouldBe("directoryPath");
    }

    /// <summary>Verifies a blank root is refused rather than resolved against the current directory.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenDirectoryPathIsBlank_ThrowsForTheDirectoryPathArgument(string directoryPath)
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonDurableStoreTarget(
            directoryPath, Identity, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends));

        exception.ParamName.ShouldBe("directoryPath");
    }

    /// <summary>Verifies a relative root is refused, because it would resolve differently per working directory.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathIsRelative_ThrowsForTheDirectoryPathArgument()
    {
        var exception = Should.Throw<ArgumentException>(() => new JsonDurableStoreTarget(
            Path.Combine("relative", "journal"),
            Identity,
            JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends));

        exception.ParamName.ShouldBe("directoryPath");
    }

    /// <summary>Verifies the absent identity is refused, because it would match a manifest written by anyone.</summary>
    [Fact]
    public void Constructor_WhenExpectedStoreInstanceIdIsDefault_ThrowsForThatArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreTarget(
            AbsoluteRoot(),
            default,
            JsonStoreOpenMode.CreateIfMissing,
            JsonStoreRecoveryMode.RecoverTornAppends));

        exception.ParamName.ShouldBe("expectedStoreInstanceId");
    }

    /// <summary>Verifies an undefined open mode is refused rather than treated as the permissive one.</summary>
    [Fact]
    public void Constructor_WhenOpenModeIsUndefined_ThrowsForTheOpenModeArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreTarget(
            AbsoluteRoot(), Identity, (JsonStoreOpenMode) 42, JsonStoreRecoveryMode.RecoverTornAppends));

        exception.ParamName.ShouldBe("openMode");
    }

    /// <summary>Verifies an undefined recovery mode is refused rather than treated as the permissive one.</summary>
    [Fact]
    public void Constructor_WhenRecoveryModeIsUndefined_ThrowsForTheRecoveryModeArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreTarget(
            AbsoluteRoot(), Identity, JsonStoreOpenMode.CreateIfMissing, (JsonStoreRecoveryMode) 42));

        exception.ParamName.ShouldBe("recoveryMode");
    }

    /// <summary>Verifies permitting creation while refusing recovery is rejected as a contradictory selection.</summary>
    /// <remarks>
    /// A host that lets initialization create the root has already accepted that initialization writes to it, so
    /// refusing to discard a torn trailing append would only make a crashed journal unopenable without manual repair.
    /// </remarks>
    [Fact]
    public void Constructor_WhenCreationIsCombinedWithValidationOnlyRecovery_ThrowsForTheRecoveryModeArgument()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new JsonDurableStoreTarget(
            AbsoluteRoot(), Identity, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.ValidateExact));

        exception.ParamName.ShouldBe("recoveryMode");
    }

    /// <summary>Verifies opening an existing root while refusing recovery is a permitted strict selection.</summary>
    [Fact]
    public void Constructor_WhenOpeningAnExistingRootWithValidationOnlyRecovery_RetainsBothSelections()
    {
        var target = new JsonDurableStoreTarget(
            AbsoluteRoot(), Identity, JsonStoreOpenMode.OpenExisting, JsonStoreRecoveryMode.ValidateExact);

        target.OpenMode.ShouldBe(JsonStoreOpenMode.OpenExisting);
        target.RecoveryMode.ShouldBe(JsonStoreRecoveryMode.ValidateExact);
    }

    /// <summary>Verifies the root is normalized once, so later comparisons never depend on traversal segments.</summary>
    [Fact]
    public void Constructor_WhenDirectoryPathContainsTraversal_NormalizesItOnce()
    {
        var root = AbsoluteRoot();
        var traversing = Path.Combine(root, "nested", "..");

        var target = new JsonDurableStoreTarget(
            traversing, Identity, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        target.DirectoryPath.ShouldBe(Path.GetFullPath(root));
        target.ExpectedStoreInstanceId.ShouldBe(Identity);
    }

    /// <summary>Verifies construction performs no probe, so an absent root is still a valid configuration.</summary>
    [Fact]
    public void Constructor_WhenTheRootDoesNotExist_SucceedsWithoutCreatingIt()
    {
        var root = AbsoluteRoot();

        var target = new JsonDurableStoreTarget(
            root, Identity, JsonStoreOpenMode.CreateIfMissing, JsonStoreRecoveryMode.RecoverTornAppends);

        Directory.Exists(target.DirectoryPath).ShouldBeFalse();
    }

    private static string AbsoluteRoot() =>
        Path.Combine(Path.GetTempPath(), $"agentkit-durability-json-{Guid.NewGuid():N}");
}
