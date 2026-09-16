// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

/// <summary>Verifies WorkspacePatchDelete behavior and contracts.</summary>
public sealed class WorkspacePatchDeleteTests
{
    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new WorkspacePatchDelete(
            new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var path = new FileSystemPath("a.txt");
        var fingerprint = new ContentHash("sha256:old");
        var delete = new WorkspacePatchDelete(new WorkspaceMutationId(Guid.NewGuid()), path, fingerprint, SecurityTestData.Grant());
        delete.Path.ShouldBe(path);
        delete.ExpectedContentFingerprint.ShouldBe(fingerprint);
        delete.Kind.ShouldBe(WorkspacePatchEntryKind.Delete);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new WorkspacePatchDelete(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
