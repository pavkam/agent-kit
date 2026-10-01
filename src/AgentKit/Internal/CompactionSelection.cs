// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Internal;

/// <summary>The compactor and compiled policy one definition's compaction selection resolved to.</summary>
/// <param name="Compactor">
/// The compactor the loop and maintenance entry point call, or <see langword="null"/> when none is composed or the
/// selected profile disables compaction.
/// </param>
/// <param name="Policy">The profile's compiled policy attached to every request, or <see langword="null"/> when the definition selects no profile or the profile is disabled.</param>
/// <param name="Disabled"><see langword="true"/> only when the definition selects a profile that deliberately disables compaction.</param>
internal readonly record struct CompactionSelection(ICompactor? Compactor, CompactionPolicySnapshot? Policy, bool Disabled);
