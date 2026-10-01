// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Defines content-free structured logs emitted by the memory coordinator, retrieval pipeline, and profile runtime.</summary>
/// <remarks>Event identifiers 32300-32399 are owned by this package. Logs carry identities and bounded vocabularies only, never memory text, queries, candidate content, or vectors.</remarks>
internal static partial class MemoryLog
{
    /// <summary>Records that a configured policy could not be resolved.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="policyId">The policy identity.</param>
    [LoggerMessage(EventId = 32300, Level = LogLevel.Warning, Message = "Memory policy {PolicyId} is not available, so the proposal was refused.")]
    internal static partial void PolicyUnavailable(ILogger logger, string policyId);

    /// <summary>Records that a configured policy threw.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="policyId">The policy identity.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(EventId = 32301, Level = LogLevel.Warning, Message = "Memory policy {PolicyId} failed with {ErrorType}, so the proposal was refused.")]
    internal static partial void PolicyFailed(ILogger logger, string policyId, string errorType);

    /// <summary>Records that a configured event sink could not be resolved.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="sinkId">The sink identity.</param>
    /// <param name="required">Whether delivery to the sink is required.</param>
    [LoggerMessage(EventId = 32302, Level = LogLevel.Warning, Message = "Memory event sink {SinkId} is not available (required: {Required}).")]
    internal static partial void EventSinkUnavailable(ILogger logger, string sinkId, bool required);

    /// <summary>Records that a configured event sink threw.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="sinkId">The sink identity.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(EventId = 32303, Level = LogLevel.Warning, Message = "Memory event sink {SinkId} failed with {ErrorType}.")]
    internal static partial void EventSinkFailed(ILogger logger, string sinkId, string errorType);

    /// <summary>Records that a memory profile runtime could not be activated.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="profile">The requested profile key.</param>
    /// <param name="version">The requested profile version.</param>
    /// <param name="kind">The bounded failure class.</param>
    [LoggerMessage(EventId = 32304, Level = LogLevel.Warning, Message = "Memory profile {Profile} version {Version} could not be activated: {Kind}.")]
    internal static partial void ProfileUnavailable(ILogger logger, string profile, long version, string kind);

    /// <summary>Records one terminal coordinator operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for served operations, Warning for refusals.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="memoryId">The memory concerned.</param>
    [LoggerMessage(EventId = 32310, Message = "Memory operation {Operation} ended with {Outcome} for memory {MemoryId}.")]
    internal static partial void OperationCompleted(ILogger logger, LogLevel level, string operation, string outcome, MemoryId memoryId);

    /// <summary>Records caller cancellation of a coordinator operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="memoryId">The memory concerned.</param>
    [LoggerMessage(EventId = 32311, Level = LogLevel.Information, Message = "Memory operation {Operation} was cancelled for memory {MemoryId}.")]
    internal static partial void OperationCancelled(ILogger logger, string operation, MemoryId memoryId);

    /// <summary>Records an unexpected coordinator failure by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="memoryId">The memory concerned.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(EventId = 32312, Level = LogLevel.Error, Message = "Memory operation {Operation} failed with {ErrorType} for memory {MemoryId}.")]
    internal static partial void OperationFaulted(ILogger logger, string operation, MemoryId memoryId, string errorType);

    /// <summary>Records that a committed operation's required observation could not be delivered.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="memoryId">The memory concerned.</param>
    [LoggerMessage(EventId = 32313, Level = LogLevel.Error, Message = "Memory operation {Operation} for memory {MemoryId} committed but a required event sink did not record it.")]
    internal static partial void RequiredObservationMissing(ILogger logger, string operation, MemoryId memoryId);

    /// <summary>Records one completed retrieval.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="candidates">The candidates exposed.</param>
    /// <param name="searched">The candidates sources produced.</param>
    [LoggerMessage(EventId = 32320, Level = LogLevel.Information, Message = "Retrieval {RequestId} exposed {Candidates} of {Searched} candidates.")]
    internal static partial void RetrievalCompleted(ILogger logger, RetrievalRequestId requestId, int candidates, int searched);

    /// <summary>Records one refused or failed retrieval.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="kind">The bounded failure class.</param>
    [LoggerMessage(EventId = 32321, Level = LogLevel.Warning, Message = "Retrieval {RequestId} was refused: {Kind}.")]
    internal static partial void RetrievalRejected(ILogger logger, RetrievalRequestId requestId, string kind);

    /// <summary>Records that one selected source failed or was denied.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="source">The source key.</param>
    /// <param name="reason">The bounded reason.</param>
    [LoggerMessage(EventId = 32322, Level = LogLevel.Warning, Message = "Retrieval {RequestId} source {Source} was unavailable: {Reason}.")]
    internal static partial void RetrievalSourceUnavailable(ILogger logger, RetrievalRequestId requestId, string source, string reason);

    /// <summary>Records an unexpected retrieval failure by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="errorType">The normalized exception type.</param>
    [LoggerMessage(EventId = 32323, Level = LogLevel.Error, Message = "Retrieval {RequestId} failed with {ErrorType}.")]
    internal static partial void RetrievalFaulted(ILogger logger, RetrievalRequestId requestId, string errorType);

    /// <summary>Records caller cancellation of a retrieval.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    [LoggerMessage(EventId = 32324, Level = LogLevel.Information, Message = "Retrieval {RequestId} was cancelled.")]
    internal static partial void RetrievalCancelled(ILogger logger, RetrievalRequestId requestId);

    /// <summary>Records that reranking failed and the original ranking was kept.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="reason">The bounded reason.</param>
    [LoggerMessage(EventId = 32325, Level = LogLevel.Warning, Message = "Retrieval {RequestId} kept its original ranking because reranking was unavailable: {Reason}.")]
    internal static partial void RerankDegraded(ILogger logger, RetrievalRequestId requestId, string reason);

    /// <summary>Records that a query embedding could not be produced.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="requestId">The retrieval request.</param>
    /// <param name="reason">The bounded reason.</param>
    [LoggerMessage(EventId = 32326, Level = LogLevel.Warning, Message = "Retrieval {RequestId} could not embed the query: {Reason}.")]
    internal static partial void EmbeddingUnavailable(ILogger logger, RetrievalRequestId requestId, string reason);

    /// <summary>Records one terminal document lifecycle operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for served operations, Warning for refusals.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    /// <param name="documentId">The document concerned.</param>
    [LoggerMessage(EventId = 32330, Message = "Document operation {Operation} ended with {Outcome} for document {DocumentId}.")]
    internal static partial void DocumentOperationCompleted(ILogger logger, LogLevel level, string operation, string outcome, DocumentId documentId);

    /// <summary>Records that document deletion propagation left an index or store with cleanup pending.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="documentId">The document concerned.</param>
    /// <param name="pending">The number of stores whose cleanup is still pending.</param>
    [LoggerMessage(EventId = 32331, Level = LogLevel.Warning, Message = "Deletion of document {DocumentId} left cleanup pending in {Pending} stores.")]
    internal static partial void DocumentCleanupPending(ILogger logger, DocumentId documentId, int pending);
}
