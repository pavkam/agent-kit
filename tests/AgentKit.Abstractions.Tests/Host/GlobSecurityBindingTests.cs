// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies GlobSecurityBinding behavior and contracts.</summary>
public sealed class GlobSecurityBindingTests
{
    [Fact]
    public void Fingerprint_WhenAnyAuthorizedInputChanges_ChangesEvidence()
    {
        var basePath = new FileSystemPath("src");
        var pattern = new GlobPattern("**/*.cs");
        var baseline = GlobSecurityBinding.Fingerprint(basePath, pattern, true, false, 5, 100, 10);
        var variants = new[]
        {
            GlobSecurityBinding.Fingerprint(new FileSystemPath("tests"), pattern, true, false, 5, 100, 10),
            GlobSecurityBinding.Fingerprint(basePath, new GlobPattern("**/*.md"), true, false, 5, 100, 10),
            GlobSecurityBinding.Fingerprint(basePath, pattern, false, false, 5, 100, 10),
            GlobSecurityBinding.Fingerprint(basePath, pattern, true, true, 5, 100, 10),
            GlobSecurityBinding.Fingerprint(basePath, pattern, true, false, 6, 100, 10),
            GlobSecurityBinding.Fingerprint(basePath, pattern, true, false, 5, 101, 10),
            GlobSecurityBinding.Fingerprint(basePath, pattern, true, false, 5, 100, 11),
        };
        variants.ShouldAllBe(variant => variant != baseline);
        variants.Distinct().Count().ShouldBe(variants.Length);
    }
}
