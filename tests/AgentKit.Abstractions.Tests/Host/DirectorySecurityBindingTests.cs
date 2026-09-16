// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies DirectorySecurityBinding behavior and contracts.</summary>
public sealed class DirectorySecurityBindingTests
{
    [Fact]
    public void DirectorySecurityBinding_WhenContinuationChanges_ChangesFingerprint()
    {
        var path = new FileSystemPath("src");
        var first = DirectorySecurityBinding.Fingerprint(path, 10, null);
        var resumed = DirectorySecurityBinding.Fingerprint(path, 10, new DirectoryEnumerationCursor(new ContentHash("sha256:snapshot"), 2));
        resumed.ShouldNotBe(first);
    }

    [Fact]
    public void Resource_WhenPathIsNull_ReturnsRootResource() =>
        DirectorySecurityBinding.Resource(null).ShouldBe(new ProtectedResource(ProtectedResourceKind.Directory, "."));

    [Fact]
    public void Resource_WhenPathIsProvided_ReturnsExactResource() =>
        DirectorySecurityBinding.Resource(new FileSystemPath("src")).ShouldBe(new ProtectedResource(ProtectedResourceKind.Directory, "src"));
}
