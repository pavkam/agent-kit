// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the bounded result of decoding one session-entry envelope.</summary>
/// <remarks>Unknown or unreadable data never becomes a semantic session entry.</remarks>
public abstract record SessionEntryDecodeResult;
