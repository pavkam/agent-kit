// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

/// <summary>
/// A scripted <see cref="ISessionStore"/> test double that returns
/// pre-configured results and records every call it received, so
/// <see cref="DefaultSessionCoordinator"/> behavior can be tested in
/// isolation from real storage logic.
/// </summary>
internal sealed class FakeSessionStore: ISessionStore
{
    public SessionStoreDescriptor Descriptor { get; } = new(new SessionStoreKey("fake"), durable: false);

    public Func<SessionCreateRequest, SessionCreateResult>? OnCreate { get; set; }

    public Func<SessionAppendRequest, SessionAppendResult>? OnAppend { get; set; }

    public Func<SessionBranchRequest, SessionBranchResult>? OnBranch { get; set; }

    public Func<SessionDeleteRequest, SessionDeleteResult>? OnDelete { get; set; }

    public Func<SessionOperationContext, SessionLoadResult>? OnLoad { get; set; }

    public Func<SessionReadRequest, SessionPageResult>? OnRead { get; set; }

    public List<SessionAppendRequest> ReceivedAppends { get; } = [];

    public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnCreate?.Invoke(request) ?? new SessionCreateFailed("not configured"));

    public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnLoad?.Invoke(context) ?? new SessionLoadFailed("not configured"));

    public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, CancellationToken cancellationToken = default)
    {
        ReceivedAppends.Add(request);
        return ValueTask.FromResult(OnAppend?.Invoke(request) ?? new SessionAppendFailed("not configured"));
    }

    public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnRead?.Invoke(request) ?? new SessionReadFailed("not configured"));

    public ValueTask<SessionBranchResult> CreateBranchAsync(SessionBranchRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnBranch?.Invoke(request) ?? new SessionBranchFailed("not configured"));

    public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(OnDelete?.Invoke(request) ?? new SessionDeleteFailed("not configured"));
}
