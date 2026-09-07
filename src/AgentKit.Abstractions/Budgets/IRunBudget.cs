// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The one owned budget scope handle a run receives for its entire
/// lifetime.
/// </summary>
/// <remarks>
/// This is a marker specialization of <see cref="IBudgetScope"/> identifying
/// the scope bound to a specific run's address. Out-of-run work — embedding,
/// reranking, maintenance, or delegation started outside an active run —
/// receives a child operation scope from <see cref="IBudgetAuthority"/>
/// instead; it never fabricates an <see cref="IRunBudget"/>.
/// </remarks>
public interface IRunBudget: IBudgetScope;
