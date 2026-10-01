// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Defines content-free <see cref="NetworkWebSearchProvider"/> events without queries, URLs, results, headers, or exception messages.</summary>
/// <remarks>This package owns event IDs 34400 through 34499; this type uses 34410 through 34419.</remarks>
internal static partial class NetworkWebSearchProviderLog
{
    /// <summary>Records one attempt refused before any search request was transmitted.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="providerId">The stable provider identity.</param>
    /// <param name="requestId">The search attempt identity.</param>
    /// <param name="stage">The bounded stage label at which the attempt was refused.</param>
    [LoggerMessage(34410, LogLevel.Information, "Web search provider {ProviderId} refused attempt {RequestId} at stage {Stage}.")]
    internal static partial void Denied(ILogger logger, ProviderId providerId, WebSearchRequestId requestId, string stage);

    /// <summary>Records one attempt that failed after authorization without a usable response.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="providerId">The stable provider identity.</param>
    /// <param name="requestId">The search attempt identity.</param>
    /// <param name="stage">The bounded stage label at which the attempt failed.</param>
    [LoggerMessage(34411, LogLevel.Warning, "Web search provider {ProviderId} attempt {RequestId} failed at stage {Stage}.")]
    internal static partial void Failed(ILogger logger, ProviderId providerId, WebSearchRequestId requestId, string stage);

    /// <summary>Records caller cancellation of one attempt.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="providerId">The stable provider identity.</param>
    /// <param name="requestId">The search attempt identity.</param>
    /// <param name="stage">The bounded stage label at which cancellation was observed.</param>
    [LoggerMessage(34412, LogLevel.Debug, "Web search provider {ProviderId} attempt {RequestId} was cancelled at stage {Stage}.")]
    internal static partial void Cancelled(ILogger logger, ProviderId providerId, WebSearchRequestId requestId, string stage);

    /// <summary>Records one attempt whose request was sent and answered.</summary>
    /// <param name="logger">The logger receiving the structured event.</param>
    /// <param name="providerId">The stable provider identity.</param>
    /// <param name="requestId">The search attempt identity.</param>
    /// <param name="statusCode">The HTTP status code the search service answered with.</param>
    [LoggerMessage(34413, LogLevel.Debug, "Web search provider {ProviderId} attempt {RequestId} was answered with status {StatusCode}.")]
    internal static partial void Answered(ILogger logger, ProviderId providerId, WebSearchRequestId requestId, int statusCode);
}
