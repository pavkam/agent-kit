// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Records one policy registration together with the concrete policy type, so the dispatcher resolves the exact registered implementation.</summary>
/// <param name="Registration">The declared registration.</param>
/// <param name="PolicyType">The concrete policy type.</param>
internal sealed record MemoryPolicyDeclaration(MemoryPolicyRegistration Registration, Type PolicyType);
