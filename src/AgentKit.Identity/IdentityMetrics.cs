// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Emits bounded terminal identity-operation metrics without claims or authentication evidence.</summary>
internal static class IdentityMetrics
{
    private static readonly Lock _lock = new();
    private static Counter<long>? _resolutions;
    private static Counter<long>? _derivations;

    /// <summary>Records a terminal resolution outcome.</summary>
    internal static void RecordResolution(string outcome)
    {
        try { GetResolutionCounter()?.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)); } catch (Exception) { }
    }

    /// <summary>Records a terminal derivation outcome.</summary>
    internal static void RecordDerivation(string outcome)
    {
        try { GetDerivationCounter()?.Add(1, new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome)); } catch (Exception) { }
    }

    private static Counter<long>? GetResolutionCounter()
    {
        lock (_lock)
        {
            try { return _resolutions ??= AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.IdentityResolutionCount); } catch (Exception) { return null; }
        }
    }

    private static Counter<long>? GetDerivationCounter()
    {
        lock (_lock)
        {
            try { return _derivations ??= AgentKitDiagnostics.Metrics.CreateCounter<long>(AgentKitMetricNames.IdentityDerivationCount); } catch (Exception) { return null; }
        }
    }
}
