// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies the closed FileDeleteResult hierarchy and its leaf invariants.</summary>
public sealed class FileDeleteResultTests
{
    private static readonly ResolvedFileTarget _target = new(
        new FileRootId("workspace"),
        new NormalizedRelativePath("notes.txt"),
        "/tmp/workspace/notes.txt",
        FilePathComparisonKind.Ordinal,
        new ContentHash("link"),
        new ContentHash("target"));

    [Fact]
    public void Hierarchy_EveryLeafDerivesFromFileDeleteResult()
    {
        FileDeleteResult[] results =
        [
            new FileDeleteSuccess(_target, 1),
            new FileDeleteNotFound(_target),
            new FileDeleteConflict(_target, "conflict"),
            new FileDeleteDenied("denied"),
            new FileDeleteCancelled(SideEffectCertainty.DefinitelyNotPerformed),
            new FileDeleteUnsupported(),
            new FileDeleteFailed("failed"),
        ];

        results.Select(static result => result.GetType()).Distinct().Count().ShouldBe(7);
    }

    [Fact]
    public void FileDeleteSuccess_Constructor_WhenPreviousBytesNegative_ThrowsNamingTheParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new FileDeleteSuccess(_target, -1)).ParamName.ShouldBe("previousBytes");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FileDeleteConflict_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) =>
        _ = Should.Throw<ArgumentException>(() => new FileDeleteConflict(_target, safeMessage!));

    [Fact]
    public void FileDeleteSuccess_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new FileDeleteSuccess(_target, 5);

        (original with { }).ShouldBe(original);
    }
}
