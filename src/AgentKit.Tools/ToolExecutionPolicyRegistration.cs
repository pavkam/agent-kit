// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

/// <summary>Marks one exact execution-policy reference as registered so the selector can enumerate keyed registrations without a provider scan.</summary>
/// <param name="Reference">The exact reference that is also the DI service key of the keyed <see cref="IToolExecutionPolicy"/>.</param>
internal sealed record ToolExecutionPolicyRegistration(ToolExecutionPolicyReference Reference);
