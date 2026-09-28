// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability;

/// <summary>Pairs one registered sink implementation type with its declared dispatch metadata.</summary>
/// <remarks>
/// The declaration retains the implementation type rather than an instance so the dispatcher can honor the declared
/// lifetime, resolving a scoped or transient sink per publication instead of capturing a singleton.
/// </remarks>
/// <param name="Registration">The immutable declared dispatch metadata.</param>
/// <param name="SinkType">The concrete sink implementation type to resolve for each publication.</param>
internal sealed record DurableExecutionEventSinkDeclaration(
    DurableExecutionEventSinkRegistration Registration,
    Type SinkType);
