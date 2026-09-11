// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSearchSecurityBinding behavior and contracts.</summary>
public sealed class FileSearchSecurityBindingTests
{
    [Fact]
    public void Fingerprint_WhenVisibilityOrBoundChanges_ChangesEvidence()
    {
        var pattern = new FileSearchPattern("needle", FileSearchPatternKind.Literal);
        var pathPattern = new GlobPattern("**/*");
        var baseline = FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2));
        var variants = new[]
        {
            FileSearchSecurityBinding.Fingerprint(new FileSystemPath("src"), pattern, pathPattern, true, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, new FileSearchPattern("other", FileSearchPatternKind.Literal), pathPattern, true, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, new GlobPattern("**/*.cs"), true, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, false, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, true, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 6, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 5, 101, 1_000, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 5, 100, 1_001, 10, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 5, 100, 1_000, 11, 200, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 5, 100, 1_000, 10, 201, TimeSpan.FromSeconds(2)),
            FileSearchSecurityBinding.Fingerprint(null, pattern, pathPattern, true, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(3)),
        };
        variants.ShouldAllBe(variant => variant != baseline);
        variants.Distinct().Count().ShouldBe(variants.Length);
    }
}
