// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Retrieval;

/// <summary>Declares the source-generated log events of the retrieval context contributor.</summary>
/// <remarks>Event identifiers 32400 through 32409 are owned by this package. Messages carry only identities and bounded failure classes, never query text or candidate content.</remarks>
internal static partial class RetrievalContextLog
{
    /// <summary>Logs that retrieval for one model request contributed no candidates because it did not complete.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requestId">The retrieval request identity.</param>
    /// <param name="kind">The bounded failure class name.</param>
    [LoggerMessage(EventId = 32400, Level = LogLevel.Warning, Message = "Retrieval {RequestId} contributed no context: {Kind}.")]
    internal static partial void RetrievalRefused(ILogger logger, RetrievalRequestId requestId, string kind);

    /// <summary>Logs that retrieval for one model request contributed candidates.</summary>
    /// <param name="logger">The logger.</param>
    /// <param name="requestId">The retrieval request identity.</param>
    /// <param name="candidates">The number of candidates contributed.</param>
    [LoggerMessage(EventId = 32401, Level = LogLevel.Debug, Message = "Retrieval {RequestId} contributed {Candidates} context candidates.")]
    internal static partial void RetrievalContributed(ILogger logger, RetrievalRequestId requestId, int candidates);
}
