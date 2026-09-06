// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Creates new values of one closed typed identity.
/// </summary>
/// <typeparam name="TIdentifier">
/// The identity value type produced by this generator.
/// </typeparam>
/// <remarks>
/// Implementations registered by first-party packages are thread-safe for
/// their registered lifetime and collision-resistant. Deterministic tests and
/// replay replace the registered generator with a controllable sequence.
/// Provider- or model-supplied identifiers are never produced by this
/// contract; they come from validated configuration or provider responses.
/// </remarks>
public interface IIdentifierGenerator<TIdentifier>
    where TIdentifier : struct
{
    /// <summary>
    /// Creates one new, valid <typeparamref name="TIdentifier"/> value.
    /// </summary>
    /// <returns>A newly created, non-default identity value.</returns>
    public TIdentifier Create();
}
