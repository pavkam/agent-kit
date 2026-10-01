// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Names the operation and purpose one operation-owned <see cref="IRandomizer"/> is created for.</summary>
/// <remarks>
/// This type is an immutable value object with structural equality. Deterministic factories derive an isolated stream
/// from their configured seed plus these two values, so the same operation and purpose always produce the same stream.
/// </remarks>
public sealed record RandomizerCreationRequest
{
    /// <summary>Initializes a validated creation request.</summary>
    /// <param name="operationId">The operation that will own the randomizer.</param>
    /// <param name="purpose">The nonblank purpose the randomizer serves.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> is the default identity.</exception>
    /// <exception cref="ArgumentException"><paramref name="purpose"/> is the default value.</exception>
    public RandomizerCreationRequest(OperationId operationId, RandomizerPurpose purpose)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose.Value, nameof(purpose));
        OperationId = operationId;
        Purpose = purpose;
    }

    /// <summary>Gets the operation that owns the randomizer.</summary>
    public OperationId OperationId { get; }

    /// <summary>Gets the purpose the randomizer serves.</summary>
    public RandomizerPurpose Purpose { get; }
}
