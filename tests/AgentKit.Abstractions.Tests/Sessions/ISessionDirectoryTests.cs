// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies ISessionDirectory default behavior and contracts.</summary>
public sealed class ISessionDirectoryTests
{
    [Fact]
    public void ListAsync_WhenRequestIsNull_ThrowsExactArgumentNullException()
    {
        ISessionDirectory directory = new UnsupportedDirectory();
        var exception = Should.Throw<ArgumentNullException>(() => _ = directory.ListAsync(null!).AsTask());
        exception.ParamName.ShouldBe("request");
    }

    [Fact]
    public void ListAsync_WhenCallerAlreadyCancelled_PreservesCancellationToken()
    {
        ISessionDirectory directory = new UnsupportedDirectory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var request = Request();
        Should.Throw<OperationCanceledException>(() => _ = directory.ListAsync(request, cancellation.Token).AsTask())
            .CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task ListAsync_WhenNotOverridden_ReturnsUnavailable()
    {
        ISessionDirectory directory = new UnsupportedDirectory();
        var result = await directory.ListAsync(Request(), TestContext.Current.CancellationToken);
        _ = result.ShouldBeOfType<SessionDirectoryListUnavailable>();
    }

    private static AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest> Request()
    {
        var authorization = SessionsTestData.Authorization(SessionsTestData.BeforeRun(), null);
        var listRequest = new SessionDirectoryListRequest(SessionsTestData.AgentId, SessionsTestData.Identity(), authorization, null, 10);
        return new AuthorizedSessionDirectoryRequest<SessionDirectoryListRequest>(listRequest, SessionsTestData.Grant(), Intent());
    }

    private static SecurityEnforcementIntent Intent() =>
        new(new SecurityEnforcementIntentId(Guid.Parse("b0000000-0000-0000-0000-000000000003")), null);

    private sealed class UnsupportedDirectory: ISessionDirectory
    {
        public ComponentId SecurityAudience => new("directory");
        public bool Durable => false;
        public ValueTask<SessionLocationResult> LocateAsync(AuthorizedSessionDirectoryRequest<SessionOperationContext> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionCreationLocationResult> LocateForCreateAsync(AuthorizedSessionDirectoryRequest<SessionCreateRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDirectoryWriteResult> RecordAsync(AuthorizedSessionDirectoryRequest<SessionDirectoryWriteRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDirectoryWriteResult> RecordCreateAsync(AuthorizedSessionDirectoryRequest<SessionDirectoryCreateRecordRequest> request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
