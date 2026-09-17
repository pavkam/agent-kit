// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Identity;

/// <summary>Isolates identity diagnostics from semantic resolution, derivation, and cancellation outcomes.</summary>
internal static class IdentityObservability
{
    /// <summary>Starts an identity activity while preserving ambient parentage and isolating hostile samplers.</summary>
    internal static Activity? Start(string name)
    {
        try { return AgentKitDiagnostics.Activities.StartActivity(name, ActivityKind.Internal, Activity.Current?.Context ?? default); } catch (Exception) { return null; }
    }

    /// <summary>Completes resolution diagnostics without changing the supplied result.</summary>
    internal static IdentityResolutionResult CompleteResolution(IdentityResolutionResult result, Activity? activity, ILogger logger)
    {
        var outcome = result is IdentityResolved ? "resolved" : "rejected";
        try { if (result is IdentityResolved resolved) { _ = activity?.SetTag(AgentKitTagNames.TenantId, resolved.Identity.TenantId.ToString()); _ = activity?.SetTag(AgentKitTagNames.PrincipalId, resolved.Identity.PrincipalId.ToString()); activity.SetSuccessful(outcome); } else { activity.SetFailed(outcome, ((IdentityRejected) result).Failure.Kind.ToString()); } IdentityMetrics.RecordResolution(outcome); try { IdentityLog.Resolved(logger, outcome); } catch (Exception) { } } catch (Exception) { } finally { Dispose(activity); }
        return result;
    }

    /// <summary>Completes derivation diagnostics without changing the supplied result.</summary>
    internal static IdentityResolutionResult CompleteDerivation(IdentityResolutionResult result, Activity? activity, ILogger logger)
    {
        var outcome = result is IdentityResolved ? "derived" : "rejected";
        try { if (result is IdentityRejected rejected) { activity.SetFailed(outcome, rejected.Failure.Kind.ToString()); } else { activity.SetSuccessful(outcome); } IdentityMetrics.RecordDerivation(outcome); try { IdentityLog.Derived(logger, outcome); } catch (Exception) { } } catch (Exception) { } finally { Dispose(activity); }
        return result;
    }

    /// <summary>Records cancellation without replacing its exception.</summary>
    internal static void CancelResolution(Activity? activity, ILogger logger)
    {
        try { activity.SetFailed("cancelled", nameof(OperationCanceledException)); IdentityMetrics.RecordResolution("cancelled"); try { IdentityLog.ResolveCancelled(logger); } catch (Exception) { } } catch (Exception) { } finally { Dispose(activity); }
    }

    /// <summary>Records derivation cancellation without replacing its exception.</summary>
    internal static void CancelDerivation(Activity? activity, ILogger logger)
    {
        try { activity.SetFailed("cancelled", nameof(OperationCanceledException)); IdentityMetrics.RecordDerivation("cancelled"); try { IdentityLog.DeriveCancelled(logger); } catch (Exception) { } } catch (Exception) { } finally { Dispose(activity); }
    }

    private static void Dispose(Activity? activity) { try { activity?.Dispose(); } catch (Exception) { } }
}
