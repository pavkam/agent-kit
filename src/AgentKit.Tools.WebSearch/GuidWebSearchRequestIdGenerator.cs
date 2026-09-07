// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.WebSearch;

/// <summary>Generates unpredictable web-search request identities for the default registration.</summary>
internal sealed class GuidWebSearchRequestIdGenerator: IIdentifierGenerator<WebSearchRequestId>
{
    /// <inheritdoc/>
    public WebSearchRequestId Create() => new(Guid.NewGuid());
}
