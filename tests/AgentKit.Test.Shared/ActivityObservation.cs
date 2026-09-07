// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>
/// Represents a terminal activity observation with a frozen tag map captured by a test listener.
/// Tag values retain the references emitted by <see cref="Activity"/> and are therefore a shallow
/// diagnostic snapshot; callers must not assume that mutable tag objects were copied.
/// </summary>
public sealed record ActivityObservation
{
    /// <summary>
    /// Creates a terminal activity observation from values captured after an activity stopped.
    /// </summary>
    /// <param name="operationName">The nonempty stable operation name emitted by the activity source.</param>
    /// <param name="status">The defined terminal status selected by the observed operation.</param>
    /// <param name="tags">A non-null frozen map of shallow tag values captured from the activity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="operationName"/> or <paramref name="tags"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="operationName"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is not defined.</exception>
    public ActivityObservation(
        string operationName,
        ActivityStatusCode status,
        FrozenDictionary<string, object?> tags)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentNullException.ThrowIfNull(tags);
        OperationName = operationName;
        Status = status;
        Tags = tags;
    }

    /// <summary>Gets the stable operation name captured from the observed activity source.</summary>
    /// <value>A nonempty immutable string captured when the observed activity stopped.</value>
    public string OperationName { get; }

    /// <summary>Gets the terminal status selected by the observed operation.</summary>
    /// <value>A defined immutable <see cref="ActivityStatusCode"/> value captured when the activity stopped.</value>
    public ActivityStatusCode Status { get; }

    /// <summary>
    /// Gets the frozen map of shallow tag values captured after the activity stopped.
    /// </summary>
    /// <value>
    /// An immutable mapping whose keys and membership cannot change. Values preserve the references
    /// supplied by the activity and can therefore reflect later mutation by their original owner.
    /// </value>
    public FrozenDictionary<string, object?> Tags { get; }

    /// <summary>Retrieves the captured value associated with <paramref name="key"/> when the source emitted it.</summary>
    /// <param name="key">The exact, case-sensitive activity tag name.</param>
    /// <returns>The captured tag value, or <see langword="null"/> when no such tag was emitted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or whitespace.</exception>
    public object? GetTagItem(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return Tags.GetValueOrDefault(key);
    }
}
