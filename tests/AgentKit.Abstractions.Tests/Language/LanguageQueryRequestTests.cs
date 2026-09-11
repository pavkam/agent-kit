// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageQueryRequest behavior and contracts.</summary>
public sealed class LanguageQueryRequestTests
{
    [Theory]
    [InlineData(LanguageQueryKind.Hover, false, true, false)]
    [InlineData(LanguageQueryKind.Diagnostics, true, true, false)]
    [InlineData(LanguageQueryKind.WorkspaceSymbols, true, false, false)]
    public void LanguageQueryRequest_WhenOperationShapeInvalid_ThrowsKind(LanguageQueryKind kind, bool hasPath, bool hasPosition, bool hasQuery)
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryRequest(new LanguageQueryId(Guid.Parse("10000000-0000-0000-0000-000000000001")), kind, hasPath ? new FileSystemPath("src/a.cs") : null, hasPosition ? new LanguagePosition(0, 0) : null, hasQuery ? "Agent" : null, 10, TimeSpan.FromSeconds(1), SecurityTestData.Grant()));
        exception.ParamName.ShouldBe("kind");
    }
}
