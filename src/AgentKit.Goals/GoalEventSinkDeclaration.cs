// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals;

/// <summary>Records one registered sink type with its declared identity and delivery rules.</summary>
/// <param name="Registration">The declared identity and delivery rules.</param>
/// <param name="SinkType">The concrete sink type the container constructs.</param>
internal sealed record GoalEventSinkDeclaration(GoalEventSinkRegistration Registration, Type SinkType);
