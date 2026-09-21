// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Captures one contributor registration before it is compiled into <see cref="ContextAssemblerServices"/>.</summary>
/// <param name="AssemblerKey">The assembler profile that owns the contributor.</param>
/// <param name="Registration">The contributor's stable registration metadata.</param>
/// <param name="ContributorType">The concrete <see cref="IContextContributor"/> implementation type.</param>
internal sealed record ContextContributorDeclaration(
    string AssemblerKey,
    ContextContributorRegistration Registration,
    Type ContributorType);
