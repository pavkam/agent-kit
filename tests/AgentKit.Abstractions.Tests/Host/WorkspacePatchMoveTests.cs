// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchMove behavior and contracts.</summary>
public sealed class WorkspacePatchMoveTests
{
    [Fact]
    public void WorkspacePatchMove_WhenDestinationEqualsSource_ThrowsExactParameter()
    {
        var path = new FileSystemPath("a.txt");
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new WorkspacePatchMove(new WorkspaceMutationId(Guid.NewGuid()), path, path, new ContentHash("sha256:old"), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("destinationPath");
    }

    [Fact]
    public void WorkspacePatchMove_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var source = new FileSystemPath("a.txt");
        var destination = new FileSystemPath("b.txt");
        var fingerprint = new ContentHash("sha256:old");
        var move = new WorkspacePatchMove(new WorkspaceMutationId(Guid.NewGuid()), source, destination, fingerprint, SecurityTestData.Grant());
        move.SourcePath.ShouldBe(source);
        move.DestinationPath.ShouldBe(destination);
        move.ExpectedContentFingerprint.ShouldBe(fingerprint);
        move.Kind.ShouldBe(WorkspacePatchEntryKind.Move);
    }

    [Fact]
    public void WorkspacePatchMove_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new WorkspacePatchMove(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new FileSystemPath("b.txt"), new ContentHash("sha256:old"), SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
