// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Deterministically checks the tool effects a run recorded against a <see cref="ToolEffectCriterion"/>.</summary>
/// <remarks>
/// The evaluator reads the tool calls and terminal results committed to the run messages; it never invokes a tool and never
/// consults mutable runtime state. Results record counts per configured tool, not arguments or results. The evaluator is stateless
/// and thread-safe.
/// </remarks>
public sealed class ToolEffectEvaluator: IEvaluator
{
    /// <summary>Gets the documented evaluator key, <c>tool-effect</c>.</summary>
    public static EvaluatorKey Key { get; } = new("tool-effect");

    /// <inheritdoc/>
    public EvaluatorDescriptor Descriptor { get; } = new(Key, new EvaluatorVersion(1), "Tool effects", [ToolEffectCriterion.CriterionKey], requiresFixture: false);

    /// <inheritdoc/>
    public ValueTask<EvaluationOutcome> EvaluateAsync(EvaluationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Evaluate(context));
    }

    private static EvaluationOutcome Evaluate(EvaluationContext context)
    {
        if (context.Case.Criteria.Find<ToolEffectCriterion>() is not { } criterion)
        {
            return new EvaluationUnsupported("The case declares no tool-effect criterion.");
        }

        if (context.Finished is not { } finished)
        {
            return new EvaluationSkipped("The run produced no result to inspect.");
        }

        var calls = new List<ToolCallPart>();
        var successes = new HashSet<ToolCallId>();
        foreach (var message in finished.NewMessages)
        {
            foreach (var part in message.Parts)
            {
                switch (part)
                {
                    case ToolCallPart call:
                        calls.Add(call);
                        break;
                    case ToolResultPart { Outcome.Kind: ToolCallOutcomeKind.Success } result:
                        _ = successes.Add(result.CallId);
                        break;
                    default:
                        break;
                }
            }
        }

        var evidence = ImmutableArray.CreateBuilder<EvaluationEvidence>();
        var violations = 0;
        foreach (var expectation in criterion.Required)
        {
            var matching = calls.Where(call => Matches(call, expectation.Tool) && ArgumentsMatch(call, expectation)).ToArray();
            var succeeded = matching.Count(call => successes.Contains(call.CallId));
            var satisfied = matching.Length >= expectation.MinimumCalls
                && (expectation.MaximumCalls is not { } maximum || matching.Length <= maximum)
                && (!expectation.RequireSuccess || succeeded >= expectation.MinimumCalls);
            violations += satisfied ? 0 : 1;
            evidence.Add(new EvaluationEvidence($"required:{expectation.Tool}", $"{(satisfied ? "satisfied" : "violated")} calls={matching.Length} succeeded={succeeded}"));
        }

        foreach (var tool in criterion.Forbidden)
        {
            var count = calls.Count(call => Matches(call, tool));
            violations += count == 0 ? 0 : 1;
            evidence.Add(new EvaluationEvidence($"forbidden:{tool}", $"{(count == 0 ? "clear" : "violated")} calls={count}"));
        }

        return violations == 0
            ? new EvaluationPassed(EvaluationScore.Certain(true), "Every tool-effect expectation held.", evidence.ToImmutable())
            : new EvaluationFailed(EvaluationScore.Certain(false), $"{violations} tool-effect expectation(s) were violated.", evidence.ToImmutable());
    }

    private static bool Matches(ToolCallPart call, string tool) =>
        string.Equals(call.Tool.ProviderAlias.Value, tool, StringComparison.Ordinal)
        || (call.Tool.Id is { } id && string.Equals(id.Value, tool, StringComparison.Ordinal));

    private static bool ArgumentsMatch(ToolCallPart call, ExpectedToolCall expectation) =>
        expectation.ArgumentsSubset is not { } subset || IsSubset(subset, call.Arguments);

    private static bool IsSubset(JsonElement expected, JsonElement actual)
    {
        if (expected.ValueKind == JsonValueKind.Object)
        {
            if (actual.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in expected.EnumerateObject())
            {
                if (!actual.TryGetProperty(property.Name, out var value) || !IsSubset(property.Value, value))
                {
                    return false;
                }
            }

            return true;
        }

        return JsonElement.DeepEquals(expected, actual);
    }
}
