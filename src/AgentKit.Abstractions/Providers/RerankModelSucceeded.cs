// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>A rerank attempt completed successfully.</summary>
public sealed record RerankModelSucceeded(RerankResponse Response): RerankModelResult;
