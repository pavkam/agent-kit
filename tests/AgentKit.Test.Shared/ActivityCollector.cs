// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// Collects only terminal activities selected by a source and operation-correlated predicate, even
/// though <see cref="ActivityListener"/> receives events from concurrent tests process-wide.
/// </summary>
public sealed class ActivityCollector: IDisposable
{
    private readonly ConcurrentQueue<ActivityObservation> _observations = new();
    private readonly ActivityListener _listener;

    /// <summary>
    /// Starts collecting activities from sources accepted by <paramref name="shouldListenTo"/> and
    /// retains only terminal snapshots accepted by <paramref name="shouldCollect"/>.
    /// </summary>
    /// <param name="shouldListenTo">Selects activity sources that must be sampled for this test.</param>
    /// <param name="shouldCollect">Selects fully tagged terminal activity snapshots for this operation.</param>
    /// <exception cref="ArgumentNullException">A predicate is <see langword="null"/>. Validation occurs before a listener is registered.</exception>
    public ActivityCollector(
        Func<ActivitySource, bool> shouldListenTo,
        Func<ActivityObservation, bool> shouldCollect)
    {
        ArgumentNullException.ThrowIfNull(shouldListenTo);
        ArgumentNullException.ThrowIfNull(shouldCollect);
        _listener = new ActivityListener
        {
            ShouldListenTo = shouldListenTo,
            Sample = SampleAllData,
            ActivityStopped = activity => Record(activity, shouldCollect),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    /// <summary>
    /// Returns a stable point-in-time copy of matching terminal activities in callback arrival order.
    /// Concurrent listener callbacks may append after this call returns and do not mutate the returned array;
    /// concurrent callback timing, rather than activity start time, determines the order.
    /// </summary>
    /// <returns>Activities that matched this collector's operation predicate.</returns>
    public IReadOnlyList<ActivityObservation> Snapshot() => _observations.ToArray();

    /// <summary>
    /// Stops listening and releases the process-wide listener registration.
    /// </summary>
    /// <remarks>
    /// This operation is idempotent. It does not wait for an <see cref="ActivityListener.ActivityStopped"/> callback
    /// that entered before disposal, so a concurrent callback can append one final observation.
    /// </remarks>
    public void Dispose() => _listener.Dispose();

    private static ActivitySamplingResult SampleAllData(ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;

    private void Record(Activity activity, Func<ActivityObservation, bool> shouldCollect)
    {
        var tags = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var tag in activity.TagObjects)
        {
            _ = tags.TryAdd(tag.Key, tag.Value);
        }

        var observation = new ActivityObservation(
            activity.OperationName,
            activity.Status,
            tags.ToFrozenDictionary(StringComparer.Ordinal));
        if (shouldCollect(observation))
        {
            _observations.Enqueue(observation);
        }
    }
}
