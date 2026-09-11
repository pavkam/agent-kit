// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures the normalized audience, operation, effect and resources of deferred work.</summary>
/// <remarks>This is descriptive evidence, never a grant. The effecting owner canonicalizes resources and input before construction and revalidates them at resolution. Resource identifiers are content and require authorized storage and redacted presentation.</remarks>
public sealed record ProtectedOperation
{
    /// <summary>Captures a structurally valid operation without performing it or evaluating policy.</summary>
    /// <param name="audience">The nondefault effecting component.</param>
    /// <param name="kind">The defined protected operation kind.</param>
    /// <param name="effect">The defined requested effect.</param>
    /// <param name="resources">Initialized nonempty, nonnull, structurally valid, unique ordered canonical resources.</param>
    /// <exception cref="ArgumentOutOfRangeException">Audience is default, or an operation, effect or resource kind is undefined.</exception>
    /// <exception cref="ArgumentNullException">A resource or its identifier is null.</exception>
    /// <exception cref="ArgumentException">Resources are uninitialized, empty, duplicate, or contain a blank identifier.</exception>
    public ProtectedOperation(ComponentId audience, SecurityOperationKind kind, SecurityEffect effect, ImmutableArray<ProtectedResource> resources)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(audience, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(effect);
        ArgumentException.ThrowIfDefaultOrEmpty(resources);
        HashSet<ProtectedResource> unique = [];
        foreach (var resource in resources)
        {
            ArgumentNullException.ThrowIfNull(resource, nameof(resources));
            ArgumentOutOfRangeException.ThrowIfUndefined(resource.Kind, nameof(resources));
            ArgumentException.ThrowIfNullOrWhiteSpace(resource.Identifier, nameof(resources));
            ArgumentException.ThrowIfNotEqual(unique.Add(resource), true, nameof(resources));
        }
        Audience = audience; Kind = kind; Effect = effect; Resources = resources;
    }
    /// <summary>Gets the component expected to enforce and perform the operation.</summary>
    /// <value>A nondefault audience; it grants no authority.</value>
    public ComponentId Audience { get; }
    /// <summary>Gets the normalized operation kind.</summary>
    /// <value>A defined security operation classification.</value>
    public SecurityOperationKind Kind { get; }
    /// <summary>Gets the requested effect without widening its disposition.</summary>
    /// <value>A defined effect; parent-directory creation remains a separate operation.</value>
    public SecurityEffect Effect { get; }
    /// <summary>Gets ordered canonical resource evidence.</summary>
    /// <value>A nonempty immutable collection that is content, not a safe diagnostic label.</value>
    public ImmutableArray<ProtectedResource> Resources { get; }
    /// <summary>Compares complete normalized operation evidence structurally.</summary>
    /// <param name="other">The candidate operation, or null.</param>
    /// <returns>True for equal audience, kind, effect and ordered resources.</returns>
    public bool Equals(ProtectedOperation? other) => other is not null && Audience == other.Audience && Kind == other.Kind && Effect == other.Effect && Resources.SequenceEqual(other.Resources);
    /// <summary>Hashes normalized evidence consistently with structural equality.</summary>
    /// <returns>A hash over the audience, kind, effect and ordered resources.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode(); hash.Add(Audience); hash.Add(Kind); hash.Add(Effect);
        foreach (var resource in Resources) { hash.Add(resource); }
        return hash.ToHashCode();
    }
}
