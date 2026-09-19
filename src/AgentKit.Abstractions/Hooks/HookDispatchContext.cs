// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Everything one dispatch needs beyond the closed point definition and event arguments: the captured catalog, this emission's metadata, and the owning activation lease.</summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its fields, safe to share across threads
/// without synchronization; the <see cref="Activation"/> lease it references is itself runtime-only, scoped, and
/// mutable, and is never serialized. Before invoking any hook, the dispatch kernel verifies that a closed point
/// definition's <c>Id</c> matches both <see cref="HookDispatchMetadata.Point"/> on <see cref="Dispatch"/> and the
/// event arguments' own point, and that the captured registrations in <see cref="Catalog"/> were validated
/// against that same closed definition; a mismatch fails before any hook is resolved or invoked.
/// </para>
/// <para>
/// Create instances of this type through <see cref="HookActivationScope.CreateDispatch"/> rather than directly:
/// the scope is what captures one catalog and activation lease for its declared engine, agent, run, turn, or
/// operation boundary and creates a fresh dispatch context — with fresh <see cref="HookDispatchMetadata"/> but the
/// same catalog and lease — for every individual dispatch within that boundary.
/// </para>
/// </remarks>
public sealed record HookDispatchContext
{
    /// <summary>Initializes a new instance of the <see cref="HookDispatchContext"/> record.</summary>
    /// <param name="catalog">The captured catalog this dispatch resolves registrations from.</param>
    /// <param name="dispatch">This emission's point, dispatch, causal, and timing facts.</param>
    /// <param name="activation">The activation lease owning this dispatch's hook instances and reentrancy tracker.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/>, <paramref name="dispatch"/>, or <paramref name="activation"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="dispatch"/>'s point does not match a registration profile captured by <paramref name="catalog"/>.</exception>
    public HookDispatchContext(HookCatalogSnapshot catalog, HookDispatchMetadata dispatch, IHookActivationLease activation)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(activation);

        Catalog = catalog;
        Dispatch = dispatch;
        Activation = activation;
    }

    /// <summary>Gets the captured catalog this dispatch resolves registrations from.</summary>
    public HookCatalogSnapshot Catalog { get; }

    /// <summary>Gets this emission's point, dispatch, causal, and timing facts.</summary>
    public HookDispatchMetadata Dispatch { get; }

    /// <summary>Gets the activation lease owning this dispatch's hook instances and reentrancy tracker.</summary>
    public IHookActivationLease Activation { get; }
}
