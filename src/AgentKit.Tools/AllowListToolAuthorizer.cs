// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools;

using Microsoft.Extensions.Options;

/// <summary>
/// The default <see cref="IToolAuthorizer"/>: grants a call only when its
/// resolved tool identity appears in the configured
/// <see cref="AgentToolsOptions.AllowedToolIds"/> allow-list.
/// </summary>
/// <remarks>
/// This is a fail-closed default: with no configuration, the allow-list is
/// empty and every tool call is denied. Composing an application that
/// actually wants a tool invocable requires explicitly adding its
/// <see cref="ToolId"/> to the allow-list; there is no ambient "development
/// mode" that grants everything by default.
/// </remarks>
public sealed class AllowListToolAuthorizer: IToolAuthorizer
{
    private readonly HashSet<ToolId> _allowedToolIds;

    /// <summary>Initializes a new instance of the <see cref="AllowListToolAuthorizer"/> class.</summary>
    /// <param name="options">The validated tools options carrying the allow-list.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AllowListToolAuthorizer(IOptions<AgentToolsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowedToolIds = options.Value.AllowedToolIds;
    }

    /// <inheritdoc/>
    public ValueTask<ToolAuthorizationDecision> AuthorizeAsync(
        ToolAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        ToolAuthorizationDecision decision = _allowedToolIds.Contains(request.Descriptor.Id)
            ? new ToolAuthorizationGranted()
            : new ToolAuthorizationDenied($"Tool '{request.Descriptor.Id}' is not in the configured allow-list.");

        return ValueTask.FromResult(decision);
    }
}
