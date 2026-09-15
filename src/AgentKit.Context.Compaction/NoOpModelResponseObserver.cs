// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>
/// An <see cref="IModelResponseObserver"/> that discards every streamed
/// event, used by <see cref="ModelCompactionStrategy"/> because it consumes
/// only the terminal <see cref="ModelAttemptResult"/> of its single
/// non-streaming summary request.
/// </summary>
/// <remarks>
/// The observer is stateless and shared through <see cref="Instance"/>; it
/// never inspects, buffers, or logs event content, so partial summary text
/// cannot leak through it.
/// </remarks>
internal sealed class NoOpModelResponseObserver: IModelResponseObserver
{
    /// <summary>Gets the shared, stateless instance of this observer.</summary>
    public static NoOpModelResponseObserver Instance { get; } = new();

    private NoOpModelResponseObserver()
    {
    }

    /// <inheritdoc/>
    public ValueTask OnEventAsync(ModelResponseEvent responseEvent, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
