// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language.Tests;

internal sealed class RecordingLanguageService: ILanguageIntelligenceService
{
    internal List<LanguageQueryRequest> Requests { get; } = [];

    internal LanguageQueryResult Result { get; set; } = new(
        LanguageQueryStatus.Success,
        LanguageQueryKind.Diagnostics,
        null,
        [],
        [],
        [],
        true,
        null);

    public ComponentId SecurityAudience { get; } = new("test.language");

    public ValueTask<LanguageQueryResult> QueryAsync(
        LanguageQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return ValueTask.FromResult(Result);
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(
        SecurityRequest request,
        CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return !allow
            ? ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityDenial("test.denied", "Denied.")))
            : ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(
                request.Id,
                new SecurityPolicyVersion(1),
                new SecurityGrant(
                    new GrantId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
                    request.Id,
                    request.Scope,
                    request.Identity,
                    request.Audience,
                    request.Kind,
                    request.Effect,
                    request.Resources,
                    request.InputFingerprint,
                    new SecurityPolicyVersion(1),
                    new SecurityRevocationVersion(1),
                    DateTimeOffset.UnixEpoch,
                    DateTimeOffset.UnixEpoch.AddMinutes(5),
                    1)));
    }
}

internal sealed class FixedSecurityRequestIdGenerator: IIdentifierGenerator<SecurityRequestId>
{
    public SecurityRequestId Create() => new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
}

internal sealed class FixedLanguageQueryIdGenerator: IIdentifierGenerator<LanguageQueryId>
{
    public LanguageQueryId Create() => new(Guid.Parse("30000000-0000-0000-0000-000000000003"));
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
}
