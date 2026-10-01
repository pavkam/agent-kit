// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Records one sink registration together with the concrete sink type, so the dispatcher resolves the exact registered implementation.</summary>
/// <param name="Registration">The declared registration.</param>
/// <param name="SinkType">The concrete sink type.</param>
internal sealed record MemoryEventSinkDeclaration(MemoryEventSinkRegistration Registration, Type SinkType);
