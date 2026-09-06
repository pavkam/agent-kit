// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.InMemory;

/// <summary>
/// A collision-resistant, thread-safe <see cref="IIdentifierGenerator{TIdentifier}"/>
/// that wraps a freshly generated <see cref="Guid"/> using a caller-supplied
/// factory.
/// </summary>
/// <typeparam name="TIdentifier">The Guid-backed identity value type to produce.</typeparam>
/// <remarks>
/// This is a documented, replaceable foundation default registered by
/// <c>AddInMemorySessionStore</c> for <see cref="SessionId"/> and
/// <see cref="BranchId"/> generation. Applications and tests register a
/// deterministic <see cref="IIdentifierGenerator{TIdentifier}"/> in its
/// place when reproducible identities are required.
/// </remarks>
internal sealed class GuidIdentifierGenerator<TIdentifier>: IIdentifierGenerator<TIdentifier>
    where TIdentifier : struct
{
    private readonly Func<Guid, TIdentifier> _factory;

    /// <summary>Initializes a new instance of the <see cref="GuidIdentifierGenerator{TIdentifier}"/> class.</summary>
    /// <param name="factory">Wraps a newly generated <see cref="Guid"/> into <typeparamref name="TIdentifier"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
    public GuidIdentifierGenerator(Func<Guid, TIdentifier> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc/>
    public TIdentifier Create() => _factory(Guid.NewGuid());
}
