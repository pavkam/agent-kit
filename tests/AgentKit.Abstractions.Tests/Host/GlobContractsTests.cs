// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

public sealed class GlobContractsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/src/*.cs")]
    [InlineData("src\\*.cs")]
    [InlineData("src//*.cs")]
    [InlineData("../*.cs")]
    [InlineData("src/**foo.cs")]
    [InlineData("src/[ab].cs")]
    public void GlobPattern_WhenSyntaxIsNotInPinnedDialect_ThrowsForValue(string value)
    {
        var exception = Should.Throw<ArgumentException>(() => new GlobPattern(value));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void GlobPattern_WhenSegmentCountExceedsComplexityBound_ThrowsForValue()
    {
        var value = string.Join('/', Enumerable.Repeat("x", GlobPattern.MaximumSegments + 1));

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new GlobPattern(value));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(0, 1, 1, "maximumDepth")]
    [InlineData(1, 0, 1, "maximumVisitedEntries")]
    [InlineData(1, 1, 0, "maximumResults")]
    public void GlobRequest_WhenBoundIsNotPositive_ThrowsBeforeAssignment(
        int maximumDepth,
        int maximumVisitedEntries,
        int maximumResults,
        string expectedParameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new GlobRequest(
            null,
            new GlobPattern("**/*.cs"),
            true,
            false,
            maximumDepth,
            maximumVisitedEntries,
            maximumResults,
            SecurityTestData.Grant()));

        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void GlobResult_WhenEquivalentSequencesAreSeparateInstances_IsStructurallyEqual()
    {
        var left = new GlobResult(
            GlobStatus.Success,
            [new FileSystemPath("a.cs"), new FileSystemPath("src/b.cs")],
            3,
            true,
            null);
        var right = new GlobResult(
            GlobStatus.Success,
            [new FileSystemPath("a.cs"), new FileSystemPath("src/b.cs")],
            3,
            true,
            null);

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

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
