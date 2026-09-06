// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An attempt that completed successfully, carrying the same committed
/// response delivered by the attempt's terminal
/// <see cref="ModelResponseCompleted"/> event.
/// </summary>
public sealed record ModelAttemptCompleted: ModelAttemptResult
{
    /// <summary>Initializes a new instance of the <see cref="ModelAttemptCompleted"/> record.</summary>
    /// <param name="response">The committed response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public ModelAttemptCompleted(ModelResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>Gets the committed response.</summary>
    public ModelResponse Response { get; init; }
}
