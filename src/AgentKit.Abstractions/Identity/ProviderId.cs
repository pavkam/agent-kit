// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one model or embedding provider — a specific vendor
/// integration such as OpenAI, OpenRouter, or Z.ai — independent of the
/// application-facing <c>ModelAlias</c> used to select a model from it.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Provider, API family, deployment, and model form a deliberately distinct
/// identity tuple (see <see cref="ApiFamilyId"/>, <see cref="DeploymentId"/>,
/// and <see cref="ModelId"/>): capabilities and behavior belong to that
/// exact combination, not to the provider brand alone. A
/// <see cref="ProviderId"/> comes from a concrete provider package's
/// registration, never from a local generator, and durable records,
/// telemetry, and vector metadata preserve it even when a routed request
/// was originally selected through a friendlier application alias or, for a
/// broker/router provider, actually served by a different upstream
/// provider (see <see cref="ProviderResponseIdentity.UpstreamProviderId"/>).
/// </para>
/// </remarks>
public readonly record struct ProviderId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical provider identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public ProviderId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical provider identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
