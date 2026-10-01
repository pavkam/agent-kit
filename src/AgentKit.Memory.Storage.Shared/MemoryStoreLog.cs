// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory.Storage;

/// <summary>Defines content-free structured logs emitted by every memory, document, and vector store adapter.</summary>
/// <remarks>Event identifiers 32000-32019 are owned by the shared memory-storage source. Logs carry bounded vocabularies only, never memory text, document content, queries, or vectors.</remarks>
internal static partial class MemoryStoreLog
{
    /// <summary>Records one terminal store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="level">Information for served operations, Warning for refusals.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="family">The bounded state family.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="outcome">The bounded outcome.</param>
    [LoggerMessage(EventId = 32000, Message = "Memory store {Adapter} {Family} operation {Operation} ended with {Outcome}.")]
    internal static partial void Completed(ILogger logger, LogLevel level, string adapter, string family, string operation, string outcome);

    /// <summary>Records caller cancellation of a store operation.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="family">The bounded state family.</param>
    /// <param name="operation">The bounded operation name.</param>
    [LoggerMessage(EventId = 32001, Level = LogLevel.Information, Message = "Memory store {Adapter} {Family} operation {Operation} was cancelled.")]
    internal static partial void Cancelled(ILogger logger, string adapter, string family, string operation);

    /// <summary>Records an unexpected store failure by type only.</summary>
    /// <param name="logger">The logger receiving structural fields only.</param>
    /// <param name="adapter">The bounded adapter name.</param>
    /// <param name="family">The bounded state family.</param>
    /// <param name="operation">The bounded operation name.</param>
    /// <param name="errorType">The normalized exception type, never raw exception content.</param>
    [LoggerMessage(EventId = 32002, Level = LogLevel.Error, Message = "Memory store {Adapter} {Family} operation {Operation} failed with {ErrorType}.")]
    internal static partial void Faulted(ILogger logger, string adapter, string family, string operation, string errorType);
}
