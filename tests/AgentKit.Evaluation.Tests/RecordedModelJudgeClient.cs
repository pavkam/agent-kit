// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation.Tests;

/// <summary>Replays recorded judge replies in order, standing in for a provider so judge tests run offline.</summary>
internal sealed class RecordedModelJudgeClient: IModelJudgeClient
{
    private readonly Queue<ModelJudgeResponse> _recorded;
    private readonly Lock _gate = new();

    public RecordedModelJudgeClient(params ModelJudgeResponse[] recorded) => _recorded = new Queue<ModelJudgeResponse>(recorded);

    public List<ModelJudgeRequest> Requests { get; } = [];

    public ValueTask<ModelJudgeResponse> JudgeAsync(ModelJudgeRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            Requests.Add(request);
            return ValueTask.FromResult(_recorded.Count == 0 ? throw new InvalidOperationException("No recorded reply remains.") : _recorded.Dequeue());
        }
    }

    public static ModelJudgeCompleted Reply(int score, string reason = "recorded reason", long input = 100, long output = 10) =>
        new($$"""{"score": {{score}}, "reason": "{{reason}}"}""", "recorded-provider", "recorded-model-2026", input, output);
}
