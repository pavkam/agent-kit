// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple.Tests;

/// <summary>Plays one scripted streamed chat-completions reply per request and records everything sent.</summary>
internal sealed class StubOpenAIHandler: HttpMessageHandler
{
    private readonly Queue<string> _replies = new();

    /// <summary>Initializes the stub with the replies to play, in order; the last one repeats.</summary>
    /// <param name="replies">Assistant texts, or <c>tool:{name}:{json}</c> to answer with one tool call.</param>
    public StubOpenAIHandler(params string[] replies)
    {
        foreach (var reply in replies)
        {
            _replies.Enqueue(reply);
        }
    }

    /// <summary>Gets every request body sent, in order.</summary>
    public List<string> Bodies { get; } = [];

    /// <summary>Gets every request sent, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        var reply = _replies.Count > 1 ? _replies.Dequeue() : _replies.Peek();

        var builder = new StringBuilder().Append(Chunk(/*lang=json,strict*/ """{"role":"assistant","content":""}"""));
        if (reply.StartsWith("tool:", StringComparison.Ordinal))
        {
            var parts = reply.Split(':', 3);
            var name = JsonSerializer.Serialize(parts[1]);
            var arguments = JsonSerializer.Serialize(parts[2]);
            _ = builder.Append(Chunk("{\"tool_calls\":[{\"index\":0,\"id\":\"call_1\",\"type\":\"function\",\"function\":{\"name\":" + name + ",\"arguments\":" + arguments + "}}]}"));
            _ = builder.Append(Chunk("{}", "\"tool_calls\""));
        }
        else
        {
            _ = builder.Append(Chunk($$"""{"content":{{JsonSerializer.Serialize(reply)}}}"""));
            _ = builder.Append(Chunk("{}", "\"stop\""));
        }

        _ = builder.Append("data: {\"id\":\"c\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4o-mini\",\"choices\":[],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15}}\n\n");
        _ = builder.Append("data: [DONE]\n\n");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(builder.ToString(), Encoding.UTF8, "text/event-stream") };
    }

    private static string Chunk(string delta, string finishReason = "null") =>
        $"data: {{\"id\":\"c\",\"object\":\"chat.completion.chunk\",\"created\":1,\"model\":\"gpt-4o-mini\",\"choices\":[{{\"index\":0,\"delta\":{delta},\"finish_reason\":{finishReason}}}]}}\n\n";
}
