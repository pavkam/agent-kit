// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// An identifier generator that delegates creation to a supplied function.
/// </summary>
/// <typeparam name="TIdentifier">The identity value type produced.</typeparam>
/// <remarks>
/// <para>
/// This exists so the facade can register a replaceable production default
/// for framework-created identities without pulling in a runtime package.
/// Tests replace it with a deterministic sequence through ordinary DI.
/// </para>
/// <para>
/// The supplied factory must be thread-safe, because one engine generates
/// identities for many concurrent runs. The default registration uses
/// <see cref="Guid.NewGuid"/>, which is.
/// </para>
/// </remarks>
internal sealed class DelegateIdentifierGenerator<TIdentifier>: IIdentifierGenerator<TIdentifier>
    where TIdentifier : struct
{
    private readonly Func<TIdentifier> _factory;

    /// <summary>
    /// Initializes a generator over a thread-safe factory function.
    /// </summary>
    /// <param name="factory">The thread-safe identity factory.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="factory"/> is <see langword="null"/>.
    /// </exception>
    public DelegateIdentifierGenerator(Func<TIdentifier> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc/>
    public TIdentifier Create() => _factory();
}
