// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies WorkspacePatchReplace behavior and contracts.</summary>
public sealed class WorkspacePatchReplaceTests
{
    [Fact]
    public void Constructor_WhenContentIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new WorkspacePatchReplace(
            new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), default, SecurityTestData.Grant())).ParamName.ShouldBe("content");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var path = new FileSystemPath("a.txt");
        var fingerprint = new ContentHash("sha256:old");
        var replace = new WorkspacePatchReplace(new WorkspaceMutationId(Guid.NewGuid()), path, fingerprint, [1, 2], SecurityTestData.Grant());
        replace.Path.ShouldBe(path);
        replace.ExpectedContentFingerprint.ShouldBe(fingerprint);
        replace.Content.ShouldBe([1, 2]);
        replace.Kind.ShouldBe(WorkspacePatchEntryKind.Replace);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new WorkspacePatchReplace(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), [1, 2], SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
