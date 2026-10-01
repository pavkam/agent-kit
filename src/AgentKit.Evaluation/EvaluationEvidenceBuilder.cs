// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Evaluation;

/// <summary>Builds the recorded manifest and usage summary from public catalog and usage evidence only.</summary>
internal static class EvaluationEvidenceBuilder
{
    /// <summary>Builds the manifest of one case repetition.</summary>
    /// <param name="agent">The resolved engine agent handle.</param>
    /// <param name="usage">The run usage, or <see langword="null"/> when no run finished.</param>
    /// <returns>The manifest naming the definition, profile, allowed models, and models actually used.</returns>
    internal static EvaluationRunManifest Manifest(Agent agent, RunUsage? usage)
    {
        Debug.Assert(agent is not null, "The validator resolved the agent before any case runs.");
        var used = ImmutableArray.CreateBuilder<EvaluationModelUse>();
        if (usage is not null)
        {
            foreach (var entry in usage.Entries)
            {
                if (entry.Model is not { } model)
                {
                    continue;
                }

                var use = new EvaluationModelUse(model.ProviderId.Value ?? "unknown", model.ApiFamily.Value ?? "unknown", model.ModelId.Value ?? "unknown", model.DeploymentId?.Value);
                if (!used.Contains(use))
                {
                    used.Add(use);
                }
            }
        }

        return new EvaluationRunManifest(
            agent.Id,
            agent.Definition.Revision,
            agent.CatalogVersion,
            agent.Definition.SessionProfile,
            [.. agent.Definition.Models.Candidates.Select(static candidate => candidate.Value)],
            used.ToImmutable());
    }

    /// <summary>Summarizes run usage, leaving a token dimension unknown unless every model request reported it as final.</summary>
    /// <param name="usage">The run usage.</param>
    /// <returns>The summary; never a partial sum presented as a measurement.</returns>
    internal static EvaluationUsageSummary Usage(RunUsage usage)
    {
        Debug.Assert(usage is not null, "A finished run always carries usage.");
        var requests = 0;
        long input = 0, output = 0;
        var inputKnown = true;
        var outputKnown = true;
        foreach (var entry in usage.Entries)
        {
            if (entry.Model is null)
            {
                continue;
            }

            requests++;
            if (entry.ProviderUsage is { ReportState: ModelUsageReportState.Final } reported)
            {
                inputKnown &= reported.InputTokens is not null;
                outputKnown &= reported.OutputTokens is not null;
                input = checked(input + (reported.InputTokens ?? 0));
                output = checked(output + (reported.OutputTokens ?? 0));
            }
            else
            {
                inputKnown = false;
                outputKnown = false;
            }
        }

        return requests == 0
            ? EvaluationUsageSummary.None
            : new EvaluationUsageSummary(requests, inputKnown ? input : null, outputKnown ? output : null);
    }

    /// <summary>Gets the bounded name of a run outcome.</summary>
    /// <param name="outcome">The run outcome.</param>
    /// <returns>The bounded name.</returns>
    internal static string OutcomeName(AgentRunOutcome outcome) => outcome switch
    {
        RunSucceeded => "succeeded",
        RunIdle => "idle",
        RunDeferred => "deferred",
        RunCancelled => "cancelled",
        RunLimitReached => "limit_reached",
        RunPolicyHalted => "policy_halted",
        RunFailed => "failed",
        _ => "unknown",
    };

    /// <summary>Gets the bounded name of a settlement outcome.</summary>
    /// <param name="settlement">The settlement outcome.</param>
    /// <returns>The bounded name.</returns>
    internal static string SettlementName(RunSettlementOutcome settlement) => settlement switch
    {
        RunSettlementCompleted => "completed",
        RunSettlementRecoveryRequired => "recovery_required",
        _ => "unknown",
    };
}
