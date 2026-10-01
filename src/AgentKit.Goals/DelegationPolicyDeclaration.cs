// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Records one registered policy type with its declared identity and ordering.</summary>
/// <param name="Registration">The declared identity and ordering.</param>
/// <param name="PolicyType">The concrete policy type the container constructs.</param>
internal sealed record DelegationPolicyDeclaration(DelegationPolicyRegistration Registration, Type PolicyType);
