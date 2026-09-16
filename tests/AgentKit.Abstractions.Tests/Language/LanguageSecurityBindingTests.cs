// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageSecurityBinding behavior and contracts.</summary>
public sealed class LanguageSecurityBindingTests
{
    [Fact]
    public void LanguageSecurityBinding_WhenWorkspaceQuery_UsesWorkspaceResourceAndHashesQueryText()
    {
        var id = new LanguageQueryId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var resource = LanguageSecurityBinding.Resource(LanguageQueryKind.WorkspaceSymbols, null);
        var fingerprint = LanguageSecurityBinding.Fingerprint(id, LanguageQueryKind.WorkspaceSymbols, null, null, "SensitiveSymbol", 10, TimeSpan.FromSeconds(1));
        resource.ShouldBe(new ProtectedResource(ProtectedResourceKind.Directory, "."));
        fingerprint.Value.ShouldStartWith("sha256:");
        fingerprint.Value.ShouldNotContain("SensitiveSymbol");
    }

    [Fact]
    public void Resource_WhenKindIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => LanguageSecurityBinding.Resource((LanguageQueryKind) 999, null));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Resource_WhenWorkspaceQueryHasPath_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => LanguageSecurityBinding.Resource(LanguageQueryKind.WorkspaceSymbols, new FileSystemPath("src/a.cs")));
        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void Resource_WhenDocumentQueryMissingPath_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => LanguageSecurityBinding.Resource(LanguageQueryKind.Hover, null));
        exception.ParamName.ShouldBe("path");
    }

    [Fact]
    public void Resource_WhenDocumentQueryHasPath_ReturnsFileResource()
    {
        var resource = LanguageSecurityBinding.Resource(LanguageQueryKind.Hover, new FileSystemPath("src/a.cs"));
        resource.ShouldBe(new ProtectedResource(ProtectedResourceKind.File, "src/a.cs"));
    }

    [Fact]
    public void Fingerprint_WhenRequestIsNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => LanguageSecurityBinding.Fingerprint(null!));
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void Fingerprint_WhenGivenValidatedRequest_MatchesDirectFingerprint()
    {
        var id = new LanguageQueryId(Guid.Parse("20000000-0000-0000-0000-000000000002"));
        var request = new LanguageQueryRequest(id, LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant());
        var direct = LanguageSecurityBinding.Fingerprint(id, LanguageQueryKind.WorkspaceSymbols, null, null, "symbol", 10, TimeSpan.FromSeconds(1));
        var fromRequest = LanguageSecurityBinding.Fingerprint(request);
        fromRequest.ShouldBe(direct);
    }
}
