// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Captures one keyed file-system profile registered in DI.</summary>
/// <param name="Key">The profile key.</param>
/// <param name="Capabilities">The capabilities exposed under the key.</param>
public sealed record FileSystemProfileRegistration(
    FileSystemProfileKey Key,
    FileSystemCapabilities Capabilities);
