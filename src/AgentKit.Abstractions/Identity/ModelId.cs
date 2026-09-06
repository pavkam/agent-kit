// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one model exposed by a provider — for example a specific
/// model name/snapshot string — independent of the application-facing
/// <c>ModelAlias</c> used to select it in configuration.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// <see cref="ProviderResponseIdentity"/> preserves both
/// <c>RequestedModelId</c> and <c>ResolvedModelId</c> separately, because a
/// provider is sometimes free to serve a request with a different concrete
/// model than the one an alias nominally requested (for example, a
/// versioned "latest" alias resolving to a dated snapshot). Durable
/// records, telemetry, and cost accounting always use the resolved
/// <see cref="ModelId"/> so historical data reflects what actually served
/// the request.
/// </para>
/// </remarks>
public readonly record struct ModelId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical model identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ModelId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical model identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
