// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Provides consistent terminal-state handling for AgentKit activities.</summary>
public static class ActivityExtensions
{
    extension(Activity? activity)
    {
        /// <summary>Marks a sampled operation as successfully completed.</summary>
        /// <param name="outcome">The stable normalized terminal outcome.</param>
        /// <remarks>A null activity is ignored so disabled listeners never affect control flow.</remarks>
        /// <exception cref="ArgumentException"><paramref name="outcome"/> is null, empty, or whitespace.</exception>
        public void SetSuccessful(string outcome)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
            _ = activity?.SetTag(AgentKitTagNames.Outcome, outcome);
            _ = activity?.SetStatus(ActivityStatusCode.Ok);
        }

        /// <summary>Marks a sampled operation as failed without attaching protected exception content.</summary>
        /// <param name="outcome">The stable normalized terminal outcome.</param>
        /// <param name="errorType">The stable normalized error category or exception type name.</param>
        /// <remarks>A null activity is ignored so disabled listeners never affect control flow.</remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="outcome"/> or <paramref name="errorType"/> is null, empty, or whitespace.
        /// </exception>
        public void SetFailed(string outcome, string errorType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
            ArgumentException.ThrowIfNullOrWhiteSpace(errorType);
            _ = activity?.SetTag(AgentKitTagNames.Outcome, outcome);
            _ = activity?.SetTag(AgentKitTagNames.ErrorType, errorType);
            _ = activity?.SetStatus(ActivityStatusCode.Error);
        }
    }
}
