// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies AuthorizedFileDelete behavior and contracts.</summary>
public sealed class AuthorizedFileDeleteTests
{
    [Fact]
    public void Constructor_WhenGrantNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AuthorizedFileDelete(CreateTarget(), null, grant: null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void Constructor_WhenValid_RoundTripsTargetPreconditionAndGrant()
    {
        var grant = SecurityTestData.Grant();
        var expected = new ContentHash("sha256:current");

        var operation = new AuthorizedFileDelete(CreateTarget(), expected, grant);

        operation.ResolvedTarget.ShouldBe(CreateTarget());
        operation.ExpectedTargetFingerprint.ShouldBe(expected);
        operation.Grant.ShouldBeSameAs(grant);
    }

    private static ResolvedFileTarget CreateTarget() => new(
        new FileRootId("workspace"),
        new NormalizedRelativePath("notes.txt"),
        hostTargetPath: "/tmp/workspace/notes.txt",
        FilePathComparisonKind.Ordinal,
        new ContentHash("link-evidence"),
        new ContentHash("target-fingerprint"));
}
