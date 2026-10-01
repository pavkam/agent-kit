// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Selects how the policy dispatcher combines the policies registered for a profile's policy key.</summary>
public enum MemoryAcceptanceMode
{
    /// <summary>A proposal is accepted only when at least one registered policy explicitly allows it and none denies it.</summary>
    RequireExplicitPolicyAllow = 0,

    /// <summary>A proposal is accepted unless a registered policy denies it, including when no policy is registered.</summary>
    /// <remarks>This mode removes the explicit-allow requirement. It never overrides a denial and is an explicit host choice.</remarks>
    AllowUnlessPolicyDenies = 1,
}
