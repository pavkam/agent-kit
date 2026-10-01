// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

using AgentKit;

/// <summary>Verifies DirectorySecurityBinding behavior and contracts.</summary>
public sealed class DirectorySecurityBindingTests
{
    [Fact]
    public void Fingerprint_WhenPathChanges_ChangesFingerprint() =>
        DirectorySecurityBinding.Fingerprint(new FileSystemPath("src")).ShouldNotBe(DirectorySecurityBinding.Fingerprint(new FileSystemPath("docs")));

    [Fact]
    public void Fingerprint_WhenPathIsNull_BindsTheRootDirectory() =>
        DirectorySecurityBinding.Fingerprint(null).ShouldBe(DirectorySecurityBinding.Fingerprint(null));

    [Fact]
    public void Fingerprint_WhenPathIsNull_DiffersFromAChildDirectory() =>
        DirectorySecurityBinding.Fingerprint(null).ShouldNotBe(DirectorySecurityBinding.Fingerprint(new FileSystemPath("src")));

    [Fact]
    public void Resource_WhenPathIsNull_ReturnsRootResource() =>
        DirectorySecurityBinding.Resource(null).ShouldBe(new ProtectedResource(ProtectedResourceKind.Directory, "."));

    [Fact]
    public void Resource_WhenPathIsProvided_ReturnsExactResource() =>
        DirectorySecurityBinding.Resource(new FileSystemPath("src")).ShouldBe(new ProtectedResource(ProtectedResourceKind.Directory, "src"));
}
