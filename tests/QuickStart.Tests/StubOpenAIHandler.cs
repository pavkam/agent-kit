// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace QuickStart.Tests;

/// <summary>Answers every chat-completions request with one fixed streamed assistant reply and records what was sent.</summary>
internal sealed class StubOpenAIHandler(string reply): HttpMessageHandler
{
    /// <summary>Gets every request the composition sent, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Gets every request body the composition sent, in order.</summary>
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        var body = new StringBuilder()
            .Append(Chunk(/*lang=json,strict*/ """{"role":"assistant","content":""}"""))
            .Append(Chunk($$"""{"content":{{System.Text.Json.JsonSerializer.Serialize(reply)}}}"""))
            .Append(Chunk("{}", "\"stop\""))
            .Append("data: {\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4o-mini\",\"choices\":[],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":12,\"total_tokens\":22}}\n\n")
            .Append("data: [DONE]\n\n")
            .ToString();

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
        };
    }

    private static string Chunk(string delta, string finishReason = "null") =>
        $"data: {{\"id\":\"chatcmpl-1\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4o-mini\",\"choices\":[{{\"index\":0,\"delta\":{delta},\"finish_reason\":{finishReason}}}]}}\n\n";
}
