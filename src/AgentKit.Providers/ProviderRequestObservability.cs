// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

using System.Diagnostics;

using AgentKit.Observability;

/// <summary>Shared provider send observability for leaf adapters.</summary>
public static class ProviderRequestObservability
{
    /// <summary>Starts a conversational provider send activity.</summary>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="operation">The bounded operation tag.</param>
    /// <returns>The started activity, when listeners are enabled.</returns>
    public static Activity? StartChatSend(ProviderId providerId, string operation = "chat") =>
        StartSend(AgentKitActivityNames.ProviderSend, providerId, operation);

    /// <summary>Starts an embedding provider send activity.</summary>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="operation">The bounded operation tag.</param>
    /// <returns>The started activity, when listeners are enabled.</returns>
    public static Activity? StartEmbeddingSend(ProviderId providerId, string operation = "embedding") =>
        StartSend(AgentKitActivityNames.ProviderEmbeddingSend, providerId, operation);

    /// <summary>Starts a rerank provider send activity.</summary>
    /// <param name="providerId">The provider identity.</param>
    /// <param name="operation">The bounded operation tag.</param>
    /// <returns>The started activity, when listeners are enabled.</returns>
    public static Activity? StartRerankSend(ProviderId providerId, string operation = "rerank") =>
        StartSend(AgentKitActivityNames.ProviderRerankSend, providerId, operation);

    /// <summary>Records one terminal provider request using bounded dimensions only.</summary>
    /// <param name="operation">The bounded operation tag.</param>
    /// <param name="outcome">The bounded terminal outcome.</param>
    /// <param name="duration">The observed duration.</param>
    public static void RecordRequest(string operation, string outcome, TimeSpan duration) =>
        ProviderMetrics.RecordProviderRequest(operation, outcome, duration);

    private static Activity? StartSend(string activityName, ProviderId providerId, string operation)
    {
        var activity = AgentKitDiagnostics.Activities.StartActivity(activityName);
        _ = activity?.SetTag(AgentKitTagNames.ProviderName, providerId.ToString());
        _ = activity?.SetTag(AgentKitTagNames.ProviderOperation, operation);
        return activity;
    }
}
