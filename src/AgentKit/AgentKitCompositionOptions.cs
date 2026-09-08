// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Configures bounded, provider-local AgentKit composition validation.</summary>
/// <remarks>The immutable values are captured by each provider factory or standalone build. They constrain validation work only and never add, resolve, or activate application services.</remarks>
public sealed record AgentKitCompositionOptions
{
    /// <summary>Gets the default maximum number of Microsoft DI infrastructure registrations that validation may derive for one provider build.</summary>
    /// <value><c>1024</c>, large enough for ordinary framework infrastructure while retaining a deterministic validation bound.</value>
    public const int DefaultMaximumDerivedInfrastructureRegistrations = 1024;

    /// <summary>Initializes immutable composition-validation limits.</summary>
    /// <param name="maximumDerivedInfrastructureRegistrations">The positive maximum number of closed registrations that opt-in Microsoft DI infrastructure validation may derive during one provider build.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumDerivedInfrastructureRegistrations"/> is less than one.</exception>
    public AgentKitCompositionOptions(
        int maximumDerivedInfrastructureRegistrations = DefaultMaximumDerivedInfrastructureRegistrations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDerivedInfrastructureRegistrations, 1);
        MaximumDerivedInfrastructureRegistrations = maximumDerivedInfrastructureRegistrations;
    }

    /// <summary>Gets the maximum derived Microsoft DI infrastructure registrations permitted for one provider build.</summary>
    /// <value>A validated positive value. Exceeding it produces a typed composition diagnostic rather than claiming the graph is infinite.</value>
    public int MaximumDerivedInfrastructureRegistrations { get; }
}
