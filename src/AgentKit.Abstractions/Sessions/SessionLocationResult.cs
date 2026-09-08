// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the terminal outcome of locating a session's authoritative store route.</summary>
/// <remarks>Consumers pattern-match the derived result and must not treat any result as authority to access a store.</remarks>
public abstract record SessionLocationResult;
