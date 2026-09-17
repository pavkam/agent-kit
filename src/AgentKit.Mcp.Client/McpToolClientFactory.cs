// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Client;

/// <summary>Connects official SDK transports to one validated reflected tool surface.</summary>
/// <typeparam name="TTools">The attributed class describing the expected remote tools.</typeparam>
public sealed class McpToolClientFactory<TTools>
    where TTools : class
{
    private readonly McpToolContract<TTools> _contract;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initializes a factory for one reflected tool contract.</summary>
    /// <param name="contract">The immutable reflected contract.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contract"/> is null.</exception>
    public McpToolClientFactory(McpToolContract<TTools> contract)
        : this(contract, NullLoggerFactory.Instance)
    {
    }

    /// <summary>Initializes a factory with the Microsoft logger factory used by AgentKit and the official SDK.</summary>
    /// <param name="contract">The immutable reflected contract.</param>
    /// <param name="loggerFactory">The logger factory used for content-free lifecycle diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contract"/> or <paramref name="loggerFactory"/> is null.</exception>
    public McpToolClientFactory(McpToolContract<TTools> contract, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _contract = contract;
        _loggerFactory = loggerFactory;
    }

    /// <summary>Connects, negotiates a protocol revision, and validates the initial remote catalog.</summary>
    /// <param name="transport">The caller-owned configured SDK transport; ownership transfers to the returned client.</param>
    /// <param name="versionPolicy">The version policy, or null for automatic modern-first negotiation.</param>
    /// <param name="serializerOptions">The caller-owned serializer settings copied for reflected requests and responses.</param>
    /// <param name="loggerFactory">An optional logger factory for SDK diagnostics.</param>
    /// <param name="cancellationToken">A token that cancels connection and initial catalog validation.</param>
    /// <returns>A connected typed tool client that owns the SDK session.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="transport"/> is null.</exception>
    /// <exception cref="McpToolContractMismatchException">The connected server cannot satisfy the reflected contract.</exception>
    /// <exception cref="McpProtocolVersionBelowMinimumException">
    /// <paramref name="versionPolicy"/> is <see cref="McpClientVersionPolicy.RequireAtLeast"/> and the server
    /// negotiated a revision below that floor; the connection is disposed before this throws.
    /// </exception>
    public async Task<McpToolClient<TTools>> ConnectAsync(
        IClientTransport transport,
        McpClientVersionPolicy? versionPolicy = null,
        JsonSerializerOptions? serializerOptions = null,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        var policy = versionPolicy ?? McpClientVersionPolicy.Automatic;
        // McpClientOptions.ProtocolVersion is both the requested version and the only version the SDK
        // will accept: the client requests exactly that revision and the SDK throws if the server
        // negotiates a different one. That is exact-pin behavior, not the documented "minimum
        // acceptable revision" floor RequireAtLeast promises (a server that prefers a newer revision
        // would fail the connection, and a server that only supports an older-but-still-acceptable
        // revision could never be reached). Always negotiate automatically, then enforce the floor
        // below by comparing the actually negotiated revision against MinimumVersion.
        var options = new McpClientOptions { ProtocolVersion = null };
        var effectiveLoggerFactory = loggerFactory ?? _loggerFactory;
        var logger = effectiveLoggerFactory.CreateLogger<McpToolClientFactory<TTools>>();
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.McpConnect,
            ActivityKind.Client);
        _ = activity?.SetTag(AgentKitTagNames.McpOperation, "connect");

        try
        {
            var sdkClient = await McpClient.CreateAsync(
                transport,
                options,
                effectiveLoggerFactory,
                cancellationToken).ConfigureAwait(false);

            // SdkMcpToolCaller's constructor can throw (a null or non-canonical negotiated protocol
            // version); evaluating it directly as McpToolClientActivator.CreateAsync's argument would
            // leak the already-connected sdkClient on that path, since the activator never receives a
            // caller to dispose. Once caller exists, McpToolClientActivator.CreateAsync owns its
            // disposal on every later failure.
            SdkMcpToolCaller caller;
            try
            {
                caller = new SdkMcpToolCaller(sdkClient);
            }
            catch
            {
                await sdkClient.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            var client = await McpToolClientActivator.CreateAsync(
                caller,
                _contract,
                serializerOptions,
                effectiveLoggerFactory,
                cancellationToken).ConfigureAwait(false);
            if (policy.MinimumVersion is { } minimum && client.ProtocolVersion < minimum)
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw new McpProtocolVersionBelowMinimumException(client.ProtocolVersion, minimum);
            }

            _ = activity?.SetTag(AgentKitTagNames.McpProtocolVersion, client.ProtocolVersion.ToString());
            activity.SetSuccessful("connected");
            McpClientLog.Connected(logger, client.ProtocolVersion);
            McpClientMetrics.Record("connect", "connected");
            return client;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            McpClientLog.ConnectCancelled(logger);
            McpClientMetrics.Record("connect", "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            McpClientLog.ConnectFailed(logger, errorType);
            McpClientMetrics.Record("connect", "failed");
            throw;
        }
    }
}
