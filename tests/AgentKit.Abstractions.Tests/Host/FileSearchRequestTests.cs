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
}
