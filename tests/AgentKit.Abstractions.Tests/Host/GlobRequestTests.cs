// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies GlobRequest behavior and contracts.</summary>
public sealed class GlobRequestTests
{
    [Theory]
    [InlineData(0, 1, 1, "maximumDepth")]
    [InlineData(1, 0, 1, "maximumVisitedEntries")]
    [InlineData(1, 1, 0, "maximumResults")]
    public void GlobRequest_WhenBoundIsNotPositive_ThrowsBeforeAssignment(int maximumDepth, int maximumVisitedEntries, int maximumResults, string expectedParameter)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new GlobRequest(null, new GlobPattern("**/*.cs"), true, false, maximumDepth, maximumVisitedEntries, maximumResults, SecurityTestData.Grant()));
        exception.ParamName.ShouldBe(expectedParameter);
    }
}
