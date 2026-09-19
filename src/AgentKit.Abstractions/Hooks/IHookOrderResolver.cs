// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Computes one deterministic dispatch order for a set of registrations targeting the same hook point.</summary>
/// <remarks>
/// Resolution is synchronous and side-effect-free: it depends only on the supplied registrations, never on I/O,
/// the clock, or ambient state, so the same input always yields the same output. <see cref="IHookCatalog"/> calls
/// this once per hook point while assembling a <see cref="HookCatalogSnapshot"/>.
/// </remarks>
public interface IHookOrderResolver
{
    /// <summary>Resolves one deterministic order for the supplied registrations.</summary>
    /// <param name="registrations">The registrations targeting one hook point, in discovery order.</param>
    /// <returns>The resolved order, or the diagnostics describing why no order satisfies every constraint.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="registrations"/> is default or contains a null element.
    /// </exception>
    public HookOrderResult Resolve(ImmutableArray<HookRegistrationDescriptor> registrations);
}
