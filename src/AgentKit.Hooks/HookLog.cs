// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Hooks;

/// <summary>Defines allocation-efficient, content-free hook dispatch log events.</summary>
internal static partial class HookLog
{
    /// <summary>Records successful completion of one typed dispatch without hook argument values.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="hookPoint">The stable typed lifecycle point.</param>
    /// <param name="hookDispatchId">The dispatch identity supplied by the hook arguments.</param>
    [LoggerMessage(8000, LogLevel.Debug, "Hook point {HookPoint} dispatch {HookDispatchId} completed.")]
    internal static partial void DispatchCompleted(ILogger logger, HookPointId hookPoint, HookDispatchId hookDispatchId);

    /// <summary>Records caller cancellation of one typed hook dispatch.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="hookPoint">The stable typed lifecycle point.</param>
    /// <param name="hookDispatchId">The dispatch identity supplied by the hook arguments.</param>
    [LoggerMessage(8001, LogLevel.Debug, "Hook point {HookPoint} dispatch {HookDispatchId} was cancelled.")]
    internal static partial void DispatchCancelled(ILogger logger, HookPointId hookPoint, HookDispatchId hookDispatchId);

    /// <summary>Records a dispatch failure that propagates to the owning operation.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="hookPoint">The stable typed lifecycle point.</param>
    /// <param name="hookDispatchId">The dispatch identity supplied by the hook arguments.</param>
    /// <param name="errorType">The propagating dispatch exception type.</param>
    [LoggerMessage(8002, LogLevel.Error, "Hook point {HookPoint} dispatch {HookDispatchId} failed with error type {ErrorType}.")]
    internal static partial void DispatchFailed(
        ILogger logger, HookPointId hookPoint, HookDispatchId hookDispatchId, string errorType);

    /// <summary>Records an isolated observer failure while preserving the owning dispatch outcome.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="hookPoint">The stable typed lifecycle point.</param>
    /// <param name="hookId">The configured hook identity whose failure was isolated.</param>
    /// <param name="hookDispatchId">The dispatch identity supplied by the hook arguments.</param>
    /// <param name="errorType">The isolated hook exception type.</param>
    [LoggerMessage(8003, LogLevel.Warning, "Hook {HookId} error type {ErrorType} was isolated in point {HookPoint} dispatch {HookDispatchId}.")]
    internal static partial void InvocationIsolated(
        ILogger logger, HookPointId hookPoint, HookId hookId, HookDispatchId hookDispatchId, string errorType);

    /// <summary>Records that a caller's reentrancy limit was reduced to the host's <see cref="AgentHookOptions.MaximumInvocationDepth"/>.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="hookPoint">The stable typed lifecycle point.</param>
    /// <param name="hookDispatchId">The dispatch identity supplied by the hook arguments.</param>
    /// <param name="requestedDepth">The reentrancy limit the caller asked for.</param>
    /// <param name="effectiveDepth">The host ceiling that bounds this dispatch instead.</param>
    [LoggerMessage(8004, LogLevel.Debug, "Hook point {HookPoint} dispatch {HookDispatchId} requested reentrant depth {RequestedDepth}; host ceiling clamps it to {EffectiveDepth}.")]
    internal static partial void ReentrantDepthClamped(
        ILogger logger, HookPointId hookPoint, HookDispatchId hookDispatchId, int requestedDepth, int effectiveDepth);

    /// <summary>Records that a caller's failure mode was escalated to satisfy the host's <see cref="AgentHookOptions.MinimumFailureMode"/>.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="hookPoint">The stable typed lifecycle point.</param>
    /// <param name="hookDispatchId">The dispatch identity supplied by the hook arguments.</param>
    /// <param name="requestedFailureMode">The failure mode the caller asked for.</param>
    /// <param name="effectiveFailureMode">The stricter mode applied to this dispatch.</param>
    [LoggerMessage(8005, LogLevel.Debug, "Hook point {HookPoint} dispatch {HookDispatchId} requested failure mode {RequestedFailureMode}; host minimum escalates it to {EffectiveFailureMode}.")]
    internal static partial void FailureModeEscalated(
        ILogger logger, HookPointId hookPoint, HookDispatchId hookDispatchId, HookFailureMode requestedFailureMode, HookFailureMode effectiveFailureMode);
}
