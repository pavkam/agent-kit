// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.ZAI.Tests.Fakes;

using System.Net;
using System.Net.Http;

/// <summary>
/// An <see cref="HttpMessageHandler"/> test double that returns a
/// caller-configured canned response and records every request it
/// receives, so tests never perform a real network call.
/// </summary>
internal sealed class StubHttpMessageHandler: HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    /// <summary>Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class.</summary>
    /// <param name="responder">Produces the response to return for each received request.</param>
    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    /// <summary>Gets every request this handler has received, in receipt order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Creates a handler that always serves the given fixture file as a
    /// buffered JSON response body.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to return.</param>
    /// <param name="fixtureRelativePath">The fixture file path, relative to <c>Resources</c>.</param>
    /// <returns>A configured <see cref="StubHttpMessageHandler"/>.</returns>
    public static StubHttpMessageHandler FromFixture(HttpStatusCode statusCode, string fixtureRelativePath) =>
        new(_ =>
        {
            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StreamContent(File.OpenRead(TestResources.GetPath(fixtureRelativePath))),
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return response;
        });

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_responder(request));
    }
}
