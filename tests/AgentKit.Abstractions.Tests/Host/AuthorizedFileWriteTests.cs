// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies AuthorizedFileWrite behavior and contracts.</summary>
public sealed class AuthorizedFileWriteTests
{
    [Fact]
    public void Constructor_WhenDeclaredContentLengthNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => CreateAuthorized(declaredContentLength: -1));
        exception.ParamName.ShouldBe("declaredContentLength");
    }

    [Fact]
    public void Constructor_WhenGrantNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AuthorizedFileWrite(
            CreateTarget(),
            FileWriteDisposition.CreateOnly,
            expectedTargetFingerprint: null,
            declaredContentLength: 0,
            new ContentHash("payload"),
            FileWriteAtomicityMode.Required,
            FileWriteEffectClass.WorkspaceBytes,
            grant: null!));
        exception.ParamName.ShouldBe("grant");
    }

    private static AuthorizedFileWrite CreateAuthorized(long declaredContentLength) => new(
        CreateTarget(),
        FileWriteDisposition.CreateOnly,
        expectedTargetFingerprint: null,
        declaredContentLength,
        new ContentHash("payload"),
        FileWriteAtomicityMode.Required,
        FileWriteEffectClass.WorkspaceBytes,
        SecurityTestData.Grant());

    private static ResolvedFileTarget CreateTarget() => new(
        new FileRootId("workspace"),
        new NormalizedRelativePath("notes.txt"),
        hostTargetPath: "/tmp/workspace/notes.txt",
        FilePathComparisonKind.Ordinal,
        new ContentHash("link-evidence"),
        new ContentHash("target-fingerprint"));
}
