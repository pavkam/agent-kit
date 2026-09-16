// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies AtomicFileReplaceRequest behavior and contracts.</summary>
public sealed class AtomicFileReplaceRequestTests
{
    [Fact]
    public void AtomicFileReplaceRequest_WhenContentIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new AtomicFileReplaceRequest(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), default, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void AtomicFileReplaceRequest_WhenGrantIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new AtomicFileReplaceRequest(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), [1], null!)).ParamName.ShouldBe("grant");

    [Fact]
    public void AtomicFileReplaceRequest_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var id = new WorkspaceMutationId(Guid.NewGuid());
        var path = new FileSystemPath("a.txt");
        var fingerprint = new ContentHash("sha256:old");
        var grant = SecurityTestData.Grant();
        var request = new AtomicFileReplaceRequest(id, path, fingerprint, [1, 2], grant);
        request.Id.ShouldBe(id);
        request.Path.ShouldBe(path);
        request.ExpectedContentFingerprint.ShouldBe(fingerprint);
        request.Content.ShouldBe([1, 2]);
        request.Grant.ShouldBeSameAs(grant);
    }

    [Fact]
    public void AtomicFileReplaceRequest_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new AtomicFileReplaceRequest(new WorkspaceMutationId(Guid.NewGuid()), new FileSystemPath("a.txt"), new ContentHash("sha256:old"), [1, 2], SecurityTestData.Grant());
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
