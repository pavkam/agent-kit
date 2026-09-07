// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

internal sealed partial class DefaultModelSelector
{
    /// <inheritdoc/>
    public async ValueTask<ModelSelectionResult> SelectAsync(
        ModelSelectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(AgentKitActivityNames.ModelSelect);
        _ = activity?.SetTag(AgentKitTagNames.ModelRequestId, request.ModelRequestId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.ModelCatalogVersion, request.Catalog.Version.Value);

        try
        {
            var result = await SelectCoreAsync(request, cancellationToken).ConfigureAwait(false);
            switch (result)
            {
                case ModelSelected selected:
                    _ = activity?.SetTag(
                        AgentKitTagNames.RequestModel,
                        selected.Decision.Model.Alias.ToString());
                    activity.SetSuccessful("selected");
                    ProviderMetrics.RecordSelection("selected");
                    break;

                default:
                    activity.SetSuccessful("no_compatible_model");
                    ProviderMetrics.RecordSelection("no_compatible_model");
                    break;
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            ProviderLog.ModelSelectionCancelled(_logger, request.ModelRequestId);
            ProviderMetrics.RecordSelection("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            ProviderLog.ModelSelectionFailed(_logger, request.ModelRequestId, errorType);
            ProviderMetrics.RecordSelection("failed");
            throw;
        }
    }
}
