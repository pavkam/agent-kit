// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted;

public sealed partial class ScriptedLanguageIntelligenceService
{
    /// <inheritdoc/>
    public async ValueTask<LanguageQueryResult> QueryAsync(
        LanguageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.LanguageQuery);
        _ = activity?.SetTag(AgentKitTagNames.LanguageQueryId, request.Id.ToString());
        _ = activity?.SetTag(AgentKitTagNames.LanguageQueryKind, request.Kind.ToString());

        try
        {
            var result = await QueryCoreAsync(request, cancellationToken).ConfigureAwait(false);
            var outcome = result.Status.ToString().ToLowerInvariant();
            if (result.Status == LanguageQueryStatus.Success)
            {
                activity.SetSuccessful(outcome);
            }
            else
            {
                activity.SetFailed(outcome, result.Status.ToString());
            }

            LanguageQueryLog.Completed(_logger, request.Id, request.Kind, result.Status);
            LanguageQueryMetrics.Record(request.Kind, outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            LanguageQueryLog.Completed(_logger, request.Id, request.Kind, LanguageQueryStatus.Cancelled);
            LanguageQueryMetrics.Record(request.Kind, "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            LanguageQueryLog.Failed(_logger, request.Id, request.Kind, errorType);
            LanguageQueryMetrics.Record(request.Kind, "failed");
            throw;
        }
    }
}
