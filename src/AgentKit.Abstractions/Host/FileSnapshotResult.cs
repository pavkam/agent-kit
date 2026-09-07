// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports an exact immutable byte snapshot or a typed pre-observation failure.</summary>
public sealed record FileSnapshotResult
{
    /// <summary>Initializes a snapshot result.</summary>
    /// <param name="status">The terminal status.</param>
    /// <param name="content">The complete bytes for success, otherwise empty.</param>
    /// <param name="contentFingerprint">The full-content fingerprint for success.</param>
    /// <param name="safeMessage">A non-sensitive explanation when present.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> is undefined.</exception>
    /// <exception cref="ArgumentException"><paramref name="content"/> is default.</exception>
    public FileSnapshotResult(
        FileSnapshotStatus status,
        ImmutableArray<byte> content,
        ContentHash? contentFingerprint,
        string? safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfUndefined(status);
        ArgumentException.ThrowIfDefault(content);
        Status = status;
        Content = content;
        ContentFingerprint = contentFingerprint;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the terminal status.</summary>
    public FileSnapshotStatus Status { get; init; }
    /// <summary>Gets the complete immutable file bytes when successful.</summary>
    public ImmutableArray<byte> Content { get; init; }
    /// <summary>Gets the complete-content fingerprint when successful.</summary>
    public ContentHash? ContentFingerprint { get; init; }
    /// <summary>Gets a non-sensitive explanation when present.</summary>
    public string? SafeMessage { get; init; }

    /// <inheritdoc/>
    public bool Equals(FileSnapshotResult? other) =>
        other is not null
        && Status == other.Status
        && Content.SequenceEqual(other.Content)
        && ContentFingerprint == other.ContentFingerprint
        && SafeMessage == other.SafeMessage;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        foreach (var value in Content)
        {
            hash.Add(value);
        }

        hash.Add(ContentFingerprint);
        hash.Add(SafeMessage);
        return hash.ToHashCode();
    }
}
