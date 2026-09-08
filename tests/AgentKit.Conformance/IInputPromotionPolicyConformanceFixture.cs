// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Creates one independently composed promotion policy and deterministic portable scenarios.</summary>
public interface IInputPromotionPolicyConformanceFixture: IDisposable
{
    /// <summary>Gets the publicly composed policy.</summary><value>The implementation under test.</value>
    public IInputPromotionPolicy Policy { get; }

    /// <summary>Creates a steering-boundary context with deliberately unordered steer and follow-up input.</summary>
    /// <returns>A valid bounded context.</returns>
    public InputPromotionContext CreateSteeringContext();

    /// <summary>Creates the ordered steer identities expected from <see cref="CreateSteeringContext"/>.</summary>
    /// <returns>Every eligible steer in admitted order.</returns>
    public ImmutableArray<AdmissionId> ExpectedSteeringAdmissions();

    /// <summary>Creates a context whose required selection exceeds its explicit bound.</summary>
    /// <returns>A valid context that cannot produce a complete bounded plan.</returns>
    public InputPromotionContext CreateOverLimitContext();
}
