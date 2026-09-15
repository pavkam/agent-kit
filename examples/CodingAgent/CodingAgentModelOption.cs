// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

/// <summary>Describes one OpenAI model exposed by the example's configuration UI.</summary>
/// <param name="Name">The short product name shown in controls.</param>
/// <param name="ModelId">The exact provider model identifier sent to OpenAI.</param>
/// <param name="Description">A compact workload-oriented explanation.</param>
internal sealed record CodingAgentModelOption(string Name, string ModelId, string Description)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Name} · {ModelId}";
}
