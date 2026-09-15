// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

/// <summary>
/// This package composes an agent through <see cref="SimpleAgentBuilder"/> rather than through a
/// service-collection extension, because it owns a whole composition and builds the provider itself.
/// </summary>
/// <remarks>
/// It deliberately adds no <c>AddSimpleAgent</c> registration: a host that already owns an
/// <c>IServiceCollection</c> registers the individual packages, exactly as
/// <see cref="SimpleAgentBuilder.Build"/> does internally, and adds <c>AddConversationSession</c>.
/// </remarks>
internal static class ServiceExtensions
{
}
