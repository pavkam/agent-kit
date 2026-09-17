// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Text.Json.Serialization.Metadata;

/// <summary>Constructs a typed client only after its initial remote catalog validates.</summary>
internal static class McpToolClientActivator
{
    /// <summary>Creates, validates, and publishes a typed client.</summary>
    /// <typeparam name="TTools">The reflected tool class.</typeparam>
    /// <param name="caller">The owned remote caller.</param>
    /// <param name="contract">The reflected local contract, or null to create it.</param>
    /// <param name="serializerOptions">The caller-owned JSON serializer settings to copy, or null for web defaults.</param>
    /// <param name="loggerFactory">The optional Microsoft logger factory used for content-free client diagnostics.</param>
    /// <param name="cancellationToken">A token that cancels initial catalog validation.</param>
    /// <returns>The validated typed client.</returns>
    public static async ValueTask<McpToolClient<TTools>> CreateAsync<TTools>(
        IMcpToolCaller caller,
        McpToolContract<TTools>? contract = null,
        JsonSerializerOptions? serializerOptions = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
        where TTools : class
    {
        ArgumentNullException.ThrowIfNull(caller);
        // caller ownership transfers to this activator for the duration of this call. Everything below
        // - contract construction, the serializer copy, logger creation, and the initial catalog
        // refresh - can throw while this method already owns caller; disposal must cover all of it, not
        // only the final RefreshAsync call, so a failure here never leaks the connected SDK session (and
        // for stdio transports, its child process).
        McpToolClient<TTools>? client = null;
        try
        {
            var effectiveSerializerOptions = serializerOptions is null
                ? new JsonSerializerOptions(JsonSerializerDefaults.Web)
                : new JsonSerializerOptions(serializerOptions);
            effectiveSerializerOptions.TypeInfoResolver ??= new DefaultJsonTypeInfoResolver();
            client = new McpToolClient<TTools>(
                caller,
                contract ?? new McpToolContract<TTools>(),
                effectiveSerializerOptions,
                (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<McpToolClient<TTools>>());
            _ = await client.RefreshAsync(cancellationToken).ConfigureAwait(false);
            return client;
        }
        catch
        {
            // Once client exists it owns caller, so dispose through it (exactly once); before that,
            // caller is still this method's own responsibility.
            if (client is not null)
            {
                await client.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                await caller.DisposeAsync().ConfigureAwait(false);
            }

            throw;
        }
    }
}
