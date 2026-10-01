// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Records one sink registration with its concrete type so a duplicate identity is detected at registration time.</summary>
/// <param name="Registration">The declared registration.</param>
/// <param name="SinkType">The concrete sink type registered as a singleton.</param>
internal sealed record ToolEventSinkDeclaration(ToolEventSinkRegistration Registration, Type SinkType);
