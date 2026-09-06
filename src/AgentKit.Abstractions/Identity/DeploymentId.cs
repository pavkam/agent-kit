// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Identifies one concrete deployment or endpoint of a model, such as an
/// Azure OpenAI deployment name or a regional endpoint alias, when a
/// provider distinguishes deployments from the underlying model identity.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural (ordinal,
/// textual) equality over <see cref="Value"/>. It carries no mutable state
/// itself and is safe to share and compare across threads without
/// synchronization.
/// </para>
/// <para>
/// Cloud brokers such as Azure OpenAI, Google Vertex AI, and Amazon
/// Bedrock route a request by deployment/region/identity rather than by
/// model name alone, so the same <see cref="ModelId"/> can behave
/// differently — different rate limits, different regional data residency —
/// depending on which <see cref="DeploymentId"/> served it. This value is
/// <see langword="null"/>-able wherever a provider has no concept of a
/// separate deployment, since forcing every provider integration to invent
/// one would misrepresent capability.
/// </para>
/// </remarks>
public readonly record struct DeploymentId
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentId"/> struct,
    /// validating that it carries usable identifier text.
    /// </summary>
    /// <param name="value">The non-empty canonical deployment identifier text.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="value"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public DeploymentId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Gets the canonical deployment identifier text.</summary>
    public string Value { get; }

    /// <summary>Returns the canonical identifier text.</summary>
    public override string ToString() => Value;
}
