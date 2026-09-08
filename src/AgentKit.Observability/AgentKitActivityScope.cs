// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability;

/// <summary>Owns one sampled AgentKit activity while containing failures from diagnostics listeners.</summary>
/// <remarks>
/// This single-owner scope must be disposed in LIFO order on the execution
/// context that started it. It restores the ambient parent activity even when a
/// listener throws. A missing sampled activity and listener failures are
/// observational conditions only and never change semantic control flow.
/// </remarks>
public sealed class AgentKitActivityScope: IDisposable
{
    private readonly Activity? _previous;
    private bool _disposed;

    private AgentKitActivityScope(Activity? activity, Activity? previous)
    {
        Activity = activity;
        _previous = previous;
    }

    /// <summary>Attempts to start an AgentKit activity and returns an inert scope when diagnostics are disabled or listener callbacks fail.</summary>
    /// <param name="name">The non-null, non-whitespace stable AgentKit activity name.</param>
    /// <param name="kind">The defined activity kind describing the operation's semantic role.</param>
    /// <param name="tags">Optional safe, bounded tags established before listener callbacks can observe the activity. Callers must not supply protected content.</param>
    /// <returns>A scope that owns the started activity, or an inert scope whose <see cref="Activity"/> is <see langword="null"/> when no activity could be started.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public static AgentKitActivityScope Start(
        string name,
        ActivityKind kind,
        IEnumerable<KeyValuePair<string, object?>>? tags = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative((int) kind, nameof(kind));
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int) kind, (int) ActivityKind.Consumer, nameof(kind));
        var previous = Activity.Current;
        try
        {
            var activity = AgentKitDiagnostics.Activities.StartActivity(
                name,
                kind,
                parentContext: previous?.Context ?? default,
                tags: tags);
            return new AgentKitActivityScope(activity, previous);
        }
        catch (Exception)
        {
            SafeRestore(previous);
            return new AgentKitActivityScope(null, previous);
        }
    }

    /// <summary>Gets the activity started by this scope, when diagnostics sampled it successfully.</summary>
    /// <value>The safely owned activity for bounded, non-content diagnostic enrichment, or <see langword="null"/> when diagnostics were disabled or failed.</value>
    public Activity? Activity { get; }

    /// <summary>Stops the owned activity, contains listener failures, and restores the captured ambient parent.</summary>
    /// <remarks>Repeated calls are harmless. Disposal never throws listener failures or changes the operation's semantic result.</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            Activity?.Dispose();
        }
        catch (Exception)
        {
            // Listener failure is observation-only.
        }
        finally
        {
            SafeRestore(_previous);
        }
    }

    private static void SafeRestore(Activity? activity)
    {
        try
        {
            Activity.Current = activity;
        }
        catch (Exception)
        {
            // Ambient observation callbacks cannot escape the safe scope.
        }
    }
}
