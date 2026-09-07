// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;

public sealed class LanguageContractsTests
{
    [Fact]
    public void LanguagePosition_WhenCoordinateNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguagePosition(-1, 0));

        exception.ParamName.ShouldBe("line");
    }

    [Fact]
    public void LanguageRange_WhenEndPrecedesStart_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageRange(
            new LanguagePosition(2, 0),
            new LanguagePosition(1, 9)));

        exception.ParamName.ShouldBe("end");
    }

    [Theory]
    [InlineData(LanguageQueryKind.Hover, false, true, false)]
    [InlineData(LanguageQueryKind.Diagnostics, true, true, false)]
    [InlineData(LanguageQueryKind.WorkspaceSymbols, true, false, false)]
    public void LanguageQueryRequest_WhenOperationShapeInvalid_ThrowsKind(
        LanguageQueryKind kind,
        bool hasPath,
        bool hasPosition,
        bool hasQuery)
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryRequest(
            new LanguageQueryId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            kind,
            hasPath ? new FileSystemPath("src/a.cs") : null,
            hasPosition ? new LanguagePosition(0, 0) : null,
            hasQuery ? "Agent" : null,
            10,
            TimeSpan.FromSeconds(1),
            SecurityTestData.Grant()));

        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void LanguageQueryResult_WhenSuccessfulAndEmpty_PreservesSuccessfulEmptySnapshot()
    {
        var result = new LanguageQueryResult(
            LanguageQueryStatus.Success,
            LanguageQueryKind.Diagnostics,
            null,
            [],
            [],
            [],
            true,
            null);

        result.Diagnostics.ShouldBeEmpty();
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public void LanguageQueryResult_WhenFailureHasNoMessage_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(
            LanguageQueryStatus.TimedOut,
            LanguageQueryKind.Diagnostics,
            null,
            [],
            [],
            [],
            false,
            null));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void LanguageSecurityBinding_WhenWorkspaceQuery_UsesWorkspaceResourceAndHashesQueryText()
    {
        var id = new LanguageQueryId(Guid.Parse("20000000-0000-0000-0000-000000000002"));

        var resource = LanguageSecurityBinding.Resource(LanguageQueryKind.WorkspaceSymbols, null);
        var fingerprint = LanguageSecurityBinding.Fingerprint(
            id,
            LanguageQueryKind.WorkspaceSymbols,
            null,
            null,
            "SensitiveSymbol",
            10,
            TimeSpan.FromSeconds(1));

        resource.ShouldBe(new ProtectedResource(ProtectedResourceKind.Directory, "."));
        fingerprint.Value.ShouldStartWith("sha256:");
        fingerprint.Value.ShouldNotContain("SensitiveSymbol");
    }
}
