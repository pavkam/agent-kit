// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions;

/// <summary>Provides the fail-closed authority used when a host has not selected an explicit security policy.</summary>
/// <remarks>This authority never issues grants. Hosts replace it through dependency injection; tool metadata and execution identity never weaken this default.</remarks>
public sealed class DenyAllSecurityAuthority: ISecurityAuthority
{
    private static readonly SecurityPolicyVersion _policyVersion = new(1);
    private readonly ILogger<DenyAllSecurityAuthority> _logger;

    /// <summary>Initializes the fail-closed authority with Microsoft's null logger.</summary>
    public DenyAllSecurityAuthority()
        : this(Microsoft.Extensions.Logging.Abstractions.NullLogger<DenyAllSecurityAuthority>.Instance)
    {
    }

    /// <summary>Initializes the fail-closed authority with a structured logger.</summary>
    /// <param name="logger">The logger that receives safe denial diagnostics.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
    public DenyAllSecurityAuthority(ILogger<DenyAllSecurityAuthority> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        using var activity = AgentKitDiagnostics.Activities.StartActivity(
            AgentKitActivityNames.SecurityAuthorize,
            ActivityKind.Internal,
            parentContext: Activity.Current?.Context ?? default,
            tags: new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SecurityAuthorize },
                { AgentKitTagNames.AgentId, request.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, request.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, request.Scope.Correlation.OperationId.ToString() },
                { AgentKitTagNames.SecurityRequestId, request.Id.ToString() },
                { AgentKitTagNames.SecurityOperationKind, request.Kind.ToString() },
                { AgentKitTagNames.SecurityEffect, request.Effect.ToString() },
            });
        activity.SetSuccessful("denied");
        SecurityMetrics.Decisions.Add(
            1,
            new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, "denied"),
            new KeyValuePair<string, object?>(AgentKitTagNames.SecurityOperationKind, request.Kind.ToString()),
            new KeyValuePair<string, object?>(AgentKitTagNames.SecurityEffect, request.Effect.ToString()));
        SecurityLog.AuthorizationStarted(_logger, request.Id, request.Kind, request.Effect);
        SecurityLog.AuthorizationDenied(_logger, request.Id);
        SecurityDecision decision = new SecurityDenied(
            request.Id,
            _policyVersion,
            new SecurityDenial("security.no_policy", "No security policy authorized this operation."));
        return ValueTask.FromResult(decision);
    }
}
