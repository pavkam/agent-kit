// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Provides the failure-isolating diagnostic helpers every durable storage adapter uses.</summary>
/// <remarks>
/// Instrumentation in a durable store is observational only. A clock that throws, a listener that throws, or a meter
/// that throws must leave the committed record and the value returned to the caller exactly as they were. These helpers
/// exist so that guarantee is implemented once instead of being re-derived, and slightly differently, in each adapter.
/// </remarks>
internal static class DurableStorageDiagnostics
{
    /// <summary>Reads a monotonic timestamp without letting a failing clock abort the operation being measured.</summary>
    /// <param name="timeProvider">The non-null injected clock.</param>
    /// <returns>The starting timestamp, or <see langword="null"/> when the clock could not produce one.</returns>
    internal static long? TryGetTimestamp(TimeProvider timeProvider)
    {
        Debug.Assert(timeProvider is not null, "An injected clock is required.");
        try
        {
            return timeProvider.GetTimestamp();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Measures elapsed time without letting a failing clock abort the operation being measured.</summary>
    /// <param name="timeProvider">The non-null injected clock.</param>
    /// <param name="started">The starting timestamp, or <see langword="null"/> when none was captured.</param>
    /// <returns>The measured duration, or <see langword="null"/> when no duration is available.</returns>
    internal static TimeSpan? TryGetElapsedTime(TimeProvider timeProvider, long? started)
    {
        Debug.Assert(timeProvider is not null, "An injected clock is required.");
        if (started is not { } timestamp)
        {
            return null;
        }

        try
        {
            return timeProvider.GetElapsedTime(timestamp);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Applies one package-owned activity update, suppressing any listener failure.</summary>
    /// <param name="activity">The started activity, or <see langword="null"/> when no listener is sampling.</param>
    /// <param name="action">The non-null package-owned update to apply.</param>
    internal static void SafeSetActivity(Activity? activity, Action<Activity?> action)
    {
        Debug.Assert(action is not null, "Only package-owned activity updates are applied.");
        try
        {
            action(activity);
        }
        catch
        {
            // Activity listeners cannot alter a committed durable outcome.
        }
    }

    /// <summary>Runs one observational logging or metric side effect, suppressing any failure it raises.</summary>
    /// <param name="action">The non-null observational side effect.</param>
    internal static void SafeObserve(Action action)
    {
        Debug.Assert(action is not null, "Only package-owned observational side effects are isolated.");
        try
        {
            action();
        }
        catch
        {
            // Logging and metrics are observational and cannot alter a committed durable outcome.
        }
    }

    /// <summary>Classifies one observed exception for diagnostics without exposing its message.</summary>
    /// <param name="exception">The non-null observed exception.</param>
    /// <returns>The exception's full type name, which carries no caller content.</returns>
    internal static string ErrorType(Exception exception)
    {
        Debug.Assert(exception is not null, "Only observed exceptions are classified.");
        return exception.GetType().FullName ?? exception.GetType().Name;
    }
}
