// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Expresses the normalized confidence established by trusted authentication, ordered from weakest to strongest.</summary>
public enum IdentityAssuranceLevel
{
    /// <summary>No authenticated subject was established under an explicit anonymous policy.</summary>
    Anonymous,
    /// <summary>A subject was identified through a single, lower-assurance factor.</summary>
    Basic,
    /// <summary>A subject was authenticated through a stronger or multi-factor method.</summary>
    Strong,
    /// <summary>A workload or subject was verified using hardware-backed or equivalently high-assurance evidence.</summary>
    HardwareBacked,
}
