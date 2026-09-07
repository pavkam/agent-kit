// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The selection policy itself is unusable, independently of which models are
/// configured.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="NoCompatibleModel"/>. That outcome means
/// the policy was coherent but nothing matched; this one means the policy
/// could never match anything and is a composition error to fix rather than a
/// runtime condition to fall back from.
/// </remarks>
public sealed record InvalidModelPolicy: ModelSelectionResult
{
    private readonly string _reason;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidModelPolicy"/>
    /// record.
    /// </summary>
    /// <param name="reason">
    /// A redacted, human-readable explanation of what makes the policy
    /// invalid.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="reason"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    public InvalidModelPolicy(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _reason = reason;
    }

    /// <summary>Gets the redacted explanation.</summary>
    /// <exception cref="ArgumentException">
    /// An initializer attempts to set null, empty, or whitespace-only text.
    /// </exception>
    public string Reason
    {
        get => _reason;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(Reason));
            _reason = value;
        }
    }
}
