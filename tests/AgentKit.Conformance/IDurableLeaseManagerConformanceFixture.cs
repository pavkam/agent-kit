// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates an isolated durable lease manager with a controllable clock.</summary>
/// <remarks>
/// Lease expiry is a time-dependent guarantee, so the suite advances the fixture's clock rather than waiting. A
/// fixture must drive the manager under test from that same clock; a manager reading an ambient clock cannot make
/// takeover deterministic and will fail these cases nondeterministically rather than usefully.
/// </remarks>
public interface IDurableLeaseManagerConformanceFixture
{
    /// <summary>Creates the lease manager under test, isolated from every other scenario.</summary>
    /// <returns>A non-null manager owning no leases.</returns>
    /// <remarks>Called once per scenario. Repeated calls within one scenario may return the same instance.</remarks>
    public IDurableLeaseManager CreateLeaseManager();

    /// <summary>Advances the deterministic clock the manager under test reads.</summary>
    /// <param name="delta">The positive elapsed duration to add.</param>
    public void Advance(TimeSpan delta);
}
