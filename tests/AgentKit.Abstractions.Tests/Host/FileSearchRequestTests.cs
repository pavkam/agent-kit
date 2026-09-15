// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSearchRequest behavior and contracts.</summary>
public sealed class FileSearchRequestTests
{
    [Theory]
    [InlineData(0, 1, 1, 1, 1, "maximumDepth")]
    [InlineData(1, 0, 1, 1, 1, "maximumFiles")]
    [InlineData(1, 1, 0, 1, 1, "maximumBytes")]
    [InlineData(1, 1, 1, 0, 1, "maximumMatches")]
    [InlineData(1, 1, 1, 1, 0, "maximumLineBytes")]
    public void FileSearchRequest_WhenNumericBoundIsNotPositive_ThrowsExactParameter(int maximumDepth, int maximumFiles, long maximumBytes, int maximumMatches, int maximumLineBytes, string expectedParameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchRequest(null, new FileSearchPattern("needle", FileSearchPatternKind.Literal), new GlobPattern("**/*"), true, false, maximumDepth, maximumFiles, maximumBytes, maximumMatches, maximumLineBytes, TimeSpan.FromSeconds(1), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe(expectedParameter);
    }

    [Fact]
    public void FileSearchRequest_WhenExclusionsAreDefault_NormalizesToEmpty()
    {
        var request = CreateRequest() with { ExcludedPathPatterns = default };

        request.ExcludedPathPatterns.ShouldBeEmpty();
    }

    [Fact]
    public void FileSearchRequest_WhenExclusionIsUninitialized_ThrowsBeforeAssignment()
    {
        var exception = Should.Throw<ArgumentException>(() => CreateRequest() with
        {
            ExcludedPathPatterns = [default],
        });

        exception.ParamName.ShouldBe("value");
    }

    private static FileSearchRequest CreateRequest() => new(
        null,
        new FileSearchPattern("needle", FileSearchPatternKind.Literal),
        new GlobPattern("**/*"),
        true,
        false,
        5,
        100,
        1_000,
        10,
        200,
        TimeSpan.FromSeconds(2),
        SecurityTestData.Grant());
}
