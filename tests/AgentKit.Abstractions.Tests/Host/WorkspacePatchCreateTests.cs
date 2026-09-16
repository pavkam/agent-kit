// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies WorkspacePatchCreate behavior and contracts.</summary>
public sealed class WorkspacePatchCreateTests
{
    [Fact]
    public void WorkspacePatchCreate_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new WorkspacePatchCreate(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), default, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void WorkspacePatchCreate_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var path = new FileSystemPath("a.txt");
        var create = new WorkspacePatchCreate(new WorkspaceMutationId(Guid.NewGuid()), path, [1, 2], SecurityTestData.Grant());
        create.Path.ShouldBe(path);
        create.Content.ShouldBe([1, 2]);
        create.Kind.ShouldBe(WorkspacePatchEntryKind.Create);
    }

    [Fact]
    public void WorkspacePatchCreate_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new WorkspacePatchCreate(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), [1, 2], SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
