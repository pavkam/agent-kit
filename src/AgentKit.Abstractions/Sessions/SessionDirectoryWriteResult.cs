// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the terminal outcome of conditionally recording a session location.</summary>
/// <remarks>Each outcome preserves whether a route was committed, already matched, conflicted, or unavailable without instructing callers to probe another store.</remarks>
public abstract record SessionDirectoryWriteResult;
