// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares how many validation-retry attempts one <see cref="OutputDefinition"/> allows.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality over its
/// fields, safe to share across threads without synchronization. Validation
/// retries reserve from a budget dedicated to output validation and never
/// consume a tool-retry budget.
/// </remarks>
public sealed record OutputRetryPolicy
{
    /// <summary>Gets the shared instance allowing no retry attempts: the first failure is terminal.</summary>
    public static OutputRetryPolicy None { get; } = new(0);

    /// <summary>Initializes a new instance of the <see cref="OutputRetryPolicy"/> record.</summary>
    /// <param name="maximumAttempts">The non-negative maximum number of retry attempts allowed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumAttempts"/> is negative.</exception>
    public OutputRetryPolicy(int maximumAttempts)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumAttempts);
        MaximumAttempts = maximumAttempts;
    }

    /// <summary>Gets the maximum number of retry attempts allowed.</summary>
    public int MaximumAttempts { get; init; }
}
