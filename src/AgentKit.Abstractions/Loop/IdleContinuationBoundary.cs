// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Marks a safe idle evaluation boundary with no retained turn, request, or assistant-response evidence.</summary>
/// <remarks>This boundary lets policy consider already captured causes or idle completion without fabricating a completed turn for a run that has none.</remarks>
public sealed record IdleContinuationBoundary: RunContinuationBoundary;
