// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageQueryResult behavior and contracts.</summary>
public sealed class LanguageQueryResultTests
{
    [Fact]
    public void LanguageQueryResult_WhenSuccessfulAndEmpty_PreservesSuccessfulEmptySnapshot()
    {
        var result = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [], true, null);
        result.Diagnostics.ShouldBeEmpty();
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public void LanguageQueryResult_WhenFailureHasNoMessage_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(LanguageQueryStatus.TimedOut, LanguageQueryKind.Diagnostics, null, [], [], [], false, null));
        exception.ParamName.ShouldBe("safeMessage");
    }
}
