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
}
