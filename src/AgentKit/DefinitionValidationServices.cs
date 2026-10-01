// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The optional engine services per-definition validation consults, resolved once and null when unregistered.</summary>
/// <remarks>
/// A null member means the engine-wide registration check already reported (or will report) the missing service, so
/// per-definition validation skips the dependent check rather than duplicating the diagnostic.
/// </remarks>
/// <param name="ModelCatalog">The engine-wide model catalog.</param>
/// <param name="CapabilityValidator">The model capability validator.</param>
/// <param name="BudgetProfiles">The budget profile catalog.</param>
/// <param name="SessionStores">The session store catalog.</param>
/// <param name="SecurityAuthorities">The security authority catalog, or null when the composition replaced it away.</param>
/// <param name="CapabilitySources">Every registered neutral capability profile source.</param>
internal sealed record DefinitionValidationServices(
    IModelCatalog? ModelCatalog,
    IModelCapabilityValidator? CapabilityValidator,
    IBudgetProfileCatalog? BudgetProfiles,
    ISessionStoreCatalog? SessionStores,
    ISecurityAuthorityCatalog? SecurityAuthorities,
    ImmutableArray<IAgentCapabilityProfileSource> CapabilitySources);
