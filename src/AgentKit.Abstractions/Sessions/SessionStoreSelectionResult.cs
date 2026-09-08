// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Represents the terminal result of explicit session-store selection.</summary>
/// <remarks>A successful selection identifies a composed store but is not authorization to use it. Rejections are typed so the coordinator can avoid unsafe fallback or recovery behavior.</remarks>
public abstract record SessionStoreSelectionResult;
