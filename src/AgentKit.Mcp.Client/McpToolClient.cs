// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Mcp.Client;

using System.Linq.Expressions;

/// <summary>
/// Invokes a remote MCP tool surface through expressions over the same class
/// methods used to describe the server contract.
/// </summary>
/// <typeparam name="TTools">The attributed class describing the expected remote tools.</typeparam>
/// <remarks>
/// The client owns its connected SDK session and must be asynchronously
/// disposed. Catalog refresh publishes immutable generations; an invocation
/// captures the current generation before sending its request.
/// </remarks>
public sealed class McpToolClient<TTools>: IAsyncDisposable
    where TTools : class
{
    private readonly IMcpToolCaller _caller;
    private readonly McpToolContract<TTools> _contract;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly ILogger<McpToolClient<TTools>> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private McpClientToolCatalogSnapshot? _catalog;
    private long _catalogVersion;
    private int _disposed;

    internal McpToolClient(
        IMcpToolCaller caller,
        McpToolContract<TTools> contract,
        JsonSerializerOptions serializerOptions,
        ILogger<McpToolClient<TTools>> logger)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(serializerOptions);
        ArgumentNullException.ThrowIfNull(logger);
        _caller = caller;
        _contract = contract;
        _serializerOptions = serializerOptions;
        _logger = logger;
    }

    /// <summary>Gets the negotiated MCP wire protocol revision.</summary>
    public McpProtocolVersion ProtocolVersion => _caller.ProtocolVersion;

    /// <summary>Gets the current immutable remote-tool catalog snapshot.</summary>
    /// <exception cref="InvalidOperationException">Initial catalog publication has not completed.</exception>
    public McpClientToolCatalogSnapshot Catalog => Volatile.Read(ref _catalog) ??
        throw new InvalidOperationException("The MCP tool catalog has not been published.");

    /// <summary>Invokes a reflected method that returns <see cref="Task{TResult}"/>.</summary>
    /// <typeparam name="TResponse">The response object type declared by the selected method.</typeparam>
    /// <param name="invocation">An expression calling one attributed method with its request object.</param>
    /// <param name="cancellationToken">A token that cancels waiting for the remote call.</param>
    /// <returns>The deserialized response object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invocation"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="invocation"/> is not a direct call to this contract.</exception>
    /// <exception cref="McpToolInvocationException">The remote tool fails or omits structured content.</exception>
    /// <exception cref="JsonException">The structured result does not deserialize to <typeparamref name="TResponse"/>.</exception>
    public ValueTask<TResponse> CallAsync<TResponse>(
        Expression<Func<TTools, Task<TResponse>>> invocation,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return CallCoreAsync<TResponse>(invocation.Body, cancellationToken);
    }

    /// <summary>Invokes a reflected method that returns <see cref="ValueTask{TResult}"/>.</summary>
    /// <typeparam name="TResponse">The response object type declared by the selected method.</typeparam>
    /// <param name="invocation">An expression calling one attributed method with its request object.</param>
    /// <param name="cancellationToken">A token that cancels waiting for the remote call.</param>
    /// <returns>The deserialized response object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="invocation"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="invocation"/> is not a direct call to this contract.</exception>
    /// <exception cref="McpToolInvocationException">The remote tool fails or omits structured content.</exception>
    /// <exception cref="JsonException">The structured result does not deserialize to <typeparamref name="TResponse"/>.</exception>
    public ValueTask<TResponse> CallAsync<TResponse>(
        Expression<Func<TTools, ValueTask<TResponse>>> invocation,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return CallCoreAsync<TResponse>(invocation.Body, cancellationToken);
    }

    /// <summary>Refreshes, validates, and atomically publishes the remote tool catalog.</summary>
    /// <param name="cancellationToken">A token that cancels the listing request.</param>
    /// <returns>A task that completes with the newly published immutable snapshot.</returns>
    /// <exception cref="ObjectDisposedException">This client has been disposed.</exception>
    /// <exception cref="McpToolContractMismatchException">The remote catalog omits or changes a required contract.</exception>
    public async ValueTask<McpClientToolCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.McpCatalogRefresh,
            ActivityKind.Client);
        _ = activity?.SetTag(AgentKitTagNames.McpOperation, "catalog.refresh");
        _ = activity?.SetTag(AgentKitTagNames.McpProtocolVersion, ProtocolVersion.ToString());
        var entered = false;
        try
        {
            await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            entered = true;
            var remoteTools = await _caller.ListToolsAsync(cancellationToken).ConfigureAwait(false);
            var byName = CreateRemoteIndex(remoteTools);
            var matched = ImmutableArray.CreateBuilder<McpRemoteToolDescriptor>(_contract.Methods.Length);
            foreach (var method in _contract.Methods)
            {
                if (!byName.TryGetValue(method.Name, out var remote))
                {
                    throw new McpToolContractMismatchException(
                        $"Remote MCP catalog does not contain required tool '{method.Name}' at contract version '{method.Version}'.");
                }

                if (remote.Version != method.Version)
                {
                    throw new McpToolContractMismatchException(
                        $"Remote MCP tool '{method.Name}' has contract version '{remote.Version?.ToString() ?? "<missing>"}', but local contract requires '{method.Version}'.");
                }

                matched.Add(new McpRemoteToolDescriptor(remote.Name, remote.Version.Value));
            }

            var snapshot = new McpClientToolCatalogSnapshot(
                new McpCatalogVersion(Interlocked.Increment(ref _catalogVersion)),
                ProtocolVersion,
                matched.MoveToImmutable());
            Volatile.Write(ref _catalog, snapshot);
            _ = activity?.SetTag(AgentKitTagNames.McpCatalogVersion, snapshot.Version.Value);
            activity.SetSuccessful("published");
            McpClientLog.CatalogPublished(_logger, ProtocolVersion, snapshot.Version, snapshot.Tools.Length);
            McpClientMetrics.Record("catalog.refresh", "published");
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            McpClientLog.CatalogRefreshCancelled(_logger, ProtocolVersion);
            McpClientMetrics.Record("catalog.refresh", "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            McpClientLog.CatalogRefreshFailed(_logger, ProtocolVersion, errorType);
            McpClientMetrics.Record("catalog.refresh", "failed");
            throw;
        }
        finally
        {
            if (entered)
            {
                _ = _refreshGate.Release();
            }
        }
    }

    /// <summary>Disposes the owned MCP client session once.</summary>
    /// <returns>A task that completes after the underlying SDK session has closed.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _caller.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask<TResponse> CallCoreAsync<TResponse>(
        Expression expression,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var catalog = Catalog;

        var call = expression as MethodCallExpression ??
            throw new ArgumentException("The invocation must be a direct MCP tool method call.", nameof(expression));
        var method = _contract.Resolve(call.Method);
        if (method.ResponseType != typeof(TResponse))
        {
            throw new ArgumentException(
                $"Expression response type '{typeof(TResponse)}' does not match reflected response type '{method.ResponseType}'.",
                nameof(expression));
        }

        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.McpToolCall,
            ActivityKind.Client);
        _ = activity?.SetTag(AgentKitTagNames.McpOperation, "tool.call");
        _ = activity?.SetTag(AgentKitTagNames.McpProtocolVersion, ProtocolVersion.ToString());
        _ = activity?.SetTag(AgentKitTagNames.McpCatalogVersion, catalog.Version.Value);
        _ = activity?.SetTag(AgentKitTagNames.ToolName, method.Name.Value);

        try
        {
            var requestParameter = method.Method.GetParameters().Single(
                static parameter => parameter.ParameterType != typeof(CancellationToken));
            var request = EvaluateRequest(call.Arguments[requestParameter.Position]);
            var structuredContent = await _caller.CallAsync(
                method,
                request,
                _serializerOptions,
                cancellationToken).ConfigureAwait(false);

            var response = structuredContent.Deserialize<TResponse>(_serializerOptions) ??
                throw new JsonException(
                    $"Remote MCP tool '{method.Name}' returned JSON null for non-null response '{typeof(TResponse)}'.");
            activity.SetSuccessful("completed");
            McpClientLog.ToolCallCompleted(_logger, method.Name, catalog.Version);
            McpClientMetrics.Record("tool.call", "completed");
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity.SetFailed("cancelled", nameof(OperationCanceledException));
            McpClientLog.ToolCallCancelled(_logger, method.Name, catalog.Version);
            McpClientMetrics.Record("tool.call", "cancelled");
            throw;
        }
        catch (Exception exception)
        {
            var errorType = exception.GetType().FullName ?? exception.GetType().Name;
            activity.SetFailed("failed", errorType);
            McpClientLog.ToolCallFailed(_logger, method.Name, catalog.Version, errorType);
            McpClientMetrics.Record("tool.call", "failed");
            throw;
        }
    }

    private static object EvaluateRequest(Expression expression) =>
        Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile().Invoke() ??
        throw new ArgumentException("The reflected MCP request object must not be null.", nameof(expression));

    private static Dictionary<McpToolName, McpRemoteTool> CreateRemoteIndex(IReadOnlyList<McpRemoteTool> remoteTools)
    {
        ArgumentNullException.ThrowIfNull(remoteTools);
        var result = new Dictionary<McpToolName, McpRemoteTool>();
        foreach (var remote in remoteTools)
        {
            if (!result.TryAdd(remote.Name, remote))
            {
                throw new McpToolContractMismatchException(
                    $"Remote MCP catalog advertises duplicate tool name '{remote.Name}'.");
            }
        }

        return result;
    }
}
