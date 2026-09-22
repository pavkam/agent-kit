// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies AuthorizedDirectoryEnumeration behavior and contracts.</summary>
public sealed class AuthorizedDirectoryEnumerationTests
{
    [Fact]
    public void Constructor_WhenGrantNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new AuthorizedDirectoryEnumeration(CreateTarget(), grant: null!));
        exception.ParamName.ShouldBe("grant");
    }

    private static ResolvedFileTarget CreateTarget() => new(
        new FileRootId("workspace"),
        new NormalizedRelativePath("src"),
        hostTargetPath: "/tmp/workspace/src",
        FilePathComparisonKind.Ordinal,
        new ContentHash("link-evidence"),
        new ContentHash("target-fingerprint"));
}
