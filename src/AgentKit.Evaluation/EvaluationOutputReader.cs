// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Reads the final output of a case repetition from public run evidence for the deterministic evaluators.</summary>
internal static class EvaluationOutputReader
{
    /// <summary>Gets the text a run produced: the validated text output when present, otherwise the assistant text.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The text, or an empty string when the run produced none.</returns>
    internal static string Text(EvaluationContext context)
    {
        Debug.Assert(context is not null, "Evaluators validate the context before reading it.");
        return context.Finished?.Output?.Text ?? context.AssistantText;
    }

    /// <summary>Reads the final output as one JSON value, preferring the structured output over parsed text.</summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="json">The cloned value when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the output is a JSON value.</returns>
    internal static bool TryReadJson(EvaluationContext context, out JsonElement json)
    {
        Debug.Assert(context is not null, "Evaluators validate the context before reading it.");
        if (context.Finished?.Output?.Json is { } structured)
        {
            json = structured.Clone();
            return true;
        }

        var text = Text(context);
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 64 });
                json = document.RootElement.Clone();
                return true;
            }
            catch (JsonException)
            {
                // Falls through to the not-JSON result below.
            }
        }

        json = default;
        return false;
    }

    /// <summary>Gets the bounded name of a run outcome as an expectation value.</summary>
    /// <param name="outcome">The run outcome.</param>
    /// <returns>The matching expectation, or <see langword="null"/> for an outcome shape this package does not know.</returns>
    internal static ExpectedRunOutcome? Outcome(AgentRunOutcome outcome) => outcome switch
    {
        RunSucceeded => ExpectedRunOutcome.Succeeded,
        RunIdle => ExpectedRunOutcome.Idle,
        RunDeferred => ExpectedRunOutcome.Deferred,
        RunCancelled => ExpectedRunOutcome.Cancelled,
        RunLimitReached => ExpectedRunOutcome.LimitReached,
        RunPolicyHalted => ExpectedRunOutcome.PolicyHalted,
        RunFailed => ExpectedRunOutcome.Failed,
        _ => null,
    };
}
