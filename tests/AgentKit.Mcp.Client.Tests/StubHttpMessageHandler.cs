// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client.Tests;

using System.Net.Http;

/// <summary>Answers each HTTP request from a deterministic callback and records the requests it saw.</summary>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond): HttpMessageHandler
{
    internal List<(HttpMethod Method, Uri? Uri)> Seen { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Seen.Add((request.Method, request.RequestUri));
        return Task.FromResult(respond(request));
    }
}
