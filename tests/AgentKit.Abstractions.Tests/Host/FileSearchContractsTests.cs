// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;

public sealed class FileSearchContractsTests
{
    [Fact]
    public void FileSearchPattern_WhenRegexRequiresBacktracking_RejectsPinnedEngineMismatch()
    {
        var exception = Should.Throw<ArgumentException>(() => new FileSearchPattern(
            /*lang=regex*/ "(text)\\1",
            FileSearchPatternKind.RegularExpression));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void FileSearchPattern_WhenLiteralContainsRegexSyntax_PreservesLiteralText()
    {
        var pattern = new FileSearchPattern("[literal](text)*", FileSearchPatternKind.Literal);

        pattern.Value.ShouldBe("[literal](text)*");
        pattern.Kind.ShouldBe(FileSearchPatternKind.Literal);
    }

    [Fact]
    public void FileSearchPattern_WhenPatternExceedsComplexityBound_ThrowsForValue()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchPattern(
            new string('x', FileSearchPattern.MaximumLength + 1),
            FileSearchPatternKind.Literal));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(0, 1, 1, 1, 1, "maximumDepth")]
    [InlineData(1, 0, 1, 1, 1, "maximumFiles")]
    [InlineData(1, 1, 0, 1, 1, "maximumBytes")]
    [InlineData(1, 1, 1, 0, 1, "maximumMatches")]
    [InlineData(1, 1, 1, 1, 0, "maximumLineBytes")]
    public void FileSearchRequest_WhenNumericBoundIsNotPositive_ThrowsExactParameter(
        int maximumDepth,
        int maximumFiles,
        long maximumBytes,
        int maximumMatches,
        int maximumLineBytes,
        string expectedParameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchRequest(
            null,
            new FileSearchPattern("needle", FileSearchPatternKind.Literal),
            new GlobPattern("**/*"),
            true,
            false,
            maximumDepth,
            maximumFiles,
            maximumBytes,
            maximumMatches,
            maximumLineBytes,
            TimeSpan.FromSeconds(1),
            SecurityTestData.Grant()));

        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void Fingerprint_WhenVisibilityOrBoundChanges_ChangesEvidence()
    {
        var pattern = new FileSearchPattern("needle", FileSearchPatternKind.Literal);
        var pathPattern = new GlobPattern("**/*");
        var baseline = FileSearchSecurityBinding.Fingerprint(
            null, pattern, pathPattern, true, false, 5, 100, 1_000, 10, 200, TimeSpan.FromSeconds(2));
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
