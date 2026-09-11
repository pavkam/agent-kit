// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Provides a typed replacement catalog for composition tests without reflection or discovery effects.</summary>
public sealed class CallbackToolRegistrationCatalog: IToolRegistrationCatalog
{
    /// <summary>Gets or sets the explicit selection callback.</summary>
    /// <value>Null permits only a valid empty request; tests supply nonempty behavior explicitly.</value>
    public Func<ToolDiscoveryRequest, CancellationToken, ToolDiscoverySelection>? SelectRequest { get; set; }

    /// <inheritdoc/>
    public ToolDiscoverySelection ResolveSelection(ToolDiscoveryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return SelectRequest is { } select ? select(request, cancellationToken) : new ToolDiscoverySelection(request, [], []);
    }
}
