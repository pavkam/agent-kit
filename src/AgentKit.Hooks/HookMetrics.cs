// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Records bounded hook outcomes without dispatch or registration identities.</summary>
internal static class HookMetrics
{
    private static readonly Counter<long> _dispatches = AgentKitDiagnostics.Metrics.CreateCounter<long>(
        AgentKitMetricNames.HookDispatchCount);

    /// <summary>Increments the hook dispatch count with a bounded outcome and no hook identities.</summary>
    /// <param name="outcome">The normalized terminal dispatch outcome.</param>
    internal static void RecordDispatch(string outcome) =>
        _dispatches.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
}
