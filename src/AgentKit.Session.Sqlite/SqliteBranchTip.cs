// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite;

/// <summary>The cheap branch-tip metadata needed to validate and extend a branch without decoding its entries.</summary>
/// <param name="TipSequence">The branch's current entry count; its next append lands at <c>TipSequence + 1</c>.</param>
/// <param name="TipEntryId">The identity of the last committed entry, or <see langword="null"/> for an empty branch.</param>
internal readonly record struct SqliteBranchTip(long TipSequence, SessionEntryId? TipEntryId);
