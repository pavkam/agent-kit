// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Defines allocation-efficient content-free artifact coordination events.</summary>
/// <remarks>Event identifiers 29000-29099 are owned by this package.</remarks>
internal static partial class ArtifactLog
{
    /// <summary>Records one terminal artifact operation outcome.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="level">The severity reflecting the semantic outcome.</param>
    /// <param name="coordinatorKey">The coordinator that ran the operation.</param>
    /// <param name="operation">The bounded artifact operation.</param>
    /// <param name="tenantId">The tenant when known.</param>
    /// <param name="artifactId">The artifact identity when established.</param>
    /// <param name="preparationId">The preparation identity when established.</param>
    /// <param name="outcome">The bounded semantic outcome.</param>
    [LoggerMessage(EventId = 29000, Message = "Artifact operation {Operation} on coordinator {CoordinatorKey} for tenant {TenantId}, artifact {ArtifactId}, and preparation {PreparationId} completed with outcome {Outcome}.")]
    internal static partial void Completed(ILogger logger, LogLevel level, string coordinatorKey, string operation, string? tenantId, string? artifactId, string? preparationId, string outcome);

    /// <summary>Records one unexpected artifact exception without protected content.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="coordinatorKey">The coordinator that ran the operation.</param>
    /// <param name="operation">The bounded artifact operation.</param>
    /// <param name="tenantId">The tenant when known.</param>
    /// <param name="artifactId">The artifact identity when established.</param>
    /// <param name="preparationId">The preparation identity when established.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(EventId = 29001, Level = LogLevel.Error, Message = "Artifact operation {Operation} on coordinator {CoordinatorKey} for tenant {TenantId}, artifact {ArtifactId}, and preparation {PreparationId} failed with error type {ErrorType}.")]
    internal static partial void Failed(ILogger logger, string coordinatorKey, string operation, string? tenantId, string? artifactId, string? preparationId, string errorType);

    /// <summary>Records a cancelled artifact operation.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="coordinatorKey">The coordinator that ran the operation.</param>
    /// <param name="operation">The bounded artifact operation.</param>
    [LoggerMessage(EventId = 29002, Level = LogLevel.Information, Message = "Artifact operation {Operation} on coordinator {CoordinatorKey} was cancelled.")]
    internal static partial void Cancelled(ILogger logger, string coordinatorKey, string operation);

    /// <summary>Records that an event sink failed and delivery continued.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="coordinatorKey">The coordinator whose event was delivered.</param>
    /// <param name="sinkId">The failing sink identity.</param>
    /// <param name="errorType">The stable exception type.</param>
    [LoggerMessage(EventId = 29003, Level = LogLevel.Warning, Message = "Artifact event sink {SinkId} for coordinator {CoordinatorKey} failed with error type {ErrorType}.")]
    internal static partial void EventSinkFailed(ILogger logger, string coordinatorKey, string sinkId, string errorType);

    /// <summary>Records that a declared event sink could not be resolved.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="coordinatorKey">The coordinator whose event was delivered.</param>
    /// <param name="sinkId">The missing sink identity.</param>
    [LoggerMessage(EventId = 29004, Level = LogLevel.Warning, Message = "Artifact event sink {SinkId} for coordinator {CoordinatorKey} is not registered in the container.")]
    internal static partial void EventSinkUnavailable(ILogger logger, string coordinatorKey, string sinkId);

    /// <summary>Records that reconciliation conservatively retained an artifact.</summary>
    /// <param name="logger">The configured logger.</param>
    /// <param name="coordinatorKey">The reconciling coordinator.</param>
    /// <param name="preparationId">The preparation whose intent is unresolved.</param>
    /// <param name="reason">The bounded reason the artifact was retained.</param>
    [LoggerMessage(EventId = 29005, Level = LogLevel.Information, Message = "Artifact reconciliation on coordinator {CoordinatorKey} retained preparation {PreparationId}: {Reason}.")]
    internal static partial void ReconciliationPending(ILogger logger, string coordinatorKey, string preparationId, string reason);
}
