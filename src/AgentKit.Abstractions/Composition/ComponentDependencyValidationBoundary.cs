// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Specifies which frozen registration evidence may satisfy one declared component dependency.</summary>
/// <remarks>The default requires an ordinary closed <see cref="ComponentRegistrationDescriptor"/>. The infrastructure boundary is an explicit opt-in for pure validation of Microsoft dependency-injection type or instance registrations; it never permits service activation or opaque factory inference.</remarks>
public enum ComponentDependencyValidationBoundary
{
    /// <summary>Only ordinary closed AgentKit component declarations may satisfy the dependency.</summary>
    DeclaredComponentsOnly = 0,

    /// <summary>A frozen Microsoft DI type or instance registration may supply recursively closed infrastructure evidence when no declared component matches.</summary>
    MicrosoftDependencyInjectionInfrastructure = 1,
}
