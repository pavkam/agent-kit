// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one keyed process executor profile registered in DI.</summary>
/// <param name="Key">The executor profile key.</param>
public sealed record ProcessExecutorRegistration(ProcessExecutorKey Key);
