// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Shared helpers for closed hook point-definition registration sets.</summary>
public static class HookPointDefinitionRegistrations
{
    /// <summary>Builds one point-id map and rejects incompatible duplicate identities.</summary>
    /// <param name="registrations">Every closed point registration in the composition.</param>
    /// <returns>A map keyed by <see cref="HookPointDefinitionRegistration.Point"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="registrations"/> is null.</exception>
    /// <exception cref="HookCompositionException">
    /// Two registrations share one <see cref="HookPointId"/> but disagree on closed types or invariants.
    /// </exception>
    public static Dictionary<HookPointId, HookPointDefinitionRegistration> ToDictionary(
        IReadOnlyList<HookPointDefinitionRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var byPoint = new Dictionary<HookPointId, HookPointDefinitionRegistration>(registrations.Count);
        foreach (var registration in registrations)
        {
            if (byPoint.TryGetValue(registration.Point, out var existing) && !existing.Equals(registration))
            {
                throw new HookCompositionException(
                    $"Hook point '{registration.Point}' is registered with incompatible closed types.");
            }

            byPoint[registration.Point] = registration;
        }

        return byPoint;
    }
}
