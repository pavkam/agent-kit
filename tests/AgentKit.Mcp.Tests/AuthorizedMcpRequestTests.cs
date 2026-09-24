// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Tests;

/// <summary>Verifies <see cref="AuthorizedMcpRequest"/>.</summary>
public sealed class AuthorizedMcpRequestTests
{
    [Fact]
    public void Constructor_WhenGrantIsNull_ThrowsArgumentNullException()
    {
        var request = new McpToolsListRequest(
            new McpRequestId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
            McpContractTestData.Operation());

        var exception = Should.Throw<ArgumentNullException>(() => new AuthorizedMcpRequest(request, grant: null!));
        exception.ParamName.ShouldBe("grant");
    }
}
