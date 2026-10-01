// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="NativeProviderChatSend"/> selects the profile runtime and maps selector faults.</summary>
public sealed class NativeProviderChatSendTests
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static LlmModelRequest Request(ModelDescriptor descriptor, ProtectedSemanticOperationContext? operation = null) =>
        new(
            new LlmRequestContext(
                ProviderTestData.ModelRequestId,
                descriptor,
                [],
                [],
                LlmToolChoice.Auto,
                LlmRequestSettings.Default,
                ExtensionData.Empty),
            attempt: 1,
            Now.AddMinutes(1),
            ProviderRequestOptions.Empty,
            operation ?? ProviderEgressHarness.Operation);

    private static ModelDescriptor Bound() => ProviderEgressHarness.Bind(ProviderTestData.Model("chat"));

    [Fact]
    public async Task BeginAsync_WhenDescriptorIsBound_SelectsTheRuntimeAndBuildsAnEgressCredential()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")), new Uri("https://selected.test/"));
        var descriptor = Bound();

        await using var context = await NativeProviderChatSend.BeginAsync(
            descriptor, Request(descriptor), selector, TimeProvider.System, TestContext.Current.CancellationToken);

        context.IsSuccess.ShouldBeTrue();
        context.EndpointBaseOverride.ShouldBe(new Uri("https://selected.test/"));
        var credential = context.CreateCredential(ProviderAuthorizationScheme.BearerToken).ShouldNotBeNull();
        credential.Runtime.ShouldBeSameAs(context.Lease);
    }

    [Fact]
    public async Task BeginAsync_WhenDescriptorIsUnbound_SelectsNothingAndBuildsNoEgressCredential()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")));
        var descriptor = ProviderTestData.Model("chat");

        await using var context = await NativeProviderChatSend.BeginAsync(
            descriptor, Request(descriptor), selector, TimeProvider.System, TestContext.Current.CancellationToken);

        context.IsSuccess.ShouldBeTrue();
        context.Lease.ShouldBeNull();
        context.CreateCredential(ProviderAuthorizationScheme.BearerToken).ShouldBeNull();
        selector.Selections.ShouldBe(0);
    }

    [Fact]
    public async Task DisposeAsync_WhenContextOwnsALease_DisposesItExactlyOnce()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")));
        var descriptor = Bound();
        var context = await NativeProviderChatSend.BeginAsync(
            descriptor, Request(descriptor), selector, TimeProvider.System, TestContext.Current.CancellationToken);

        await context.DisposeAsync();

        selector.Disposals.ShouldBe(1);
    }

    [Fact]
    public async Task BeginAsync_WhenCallerAlreadyCancelled_FailsWithCancellationInsteadOfThrowing()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")));
        var descriptor = Bound();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await using var context = await NativeProviderChatSend.BeginAsync(descriptor, Request(descriptor), selector, TimeProvider.System, cts.Token);

        context.IsSuccess.ShouldBeFalse();
        context.Failure.ShouldNotBeNull().Kind.ShouldBe(ProviderFailureKind.Cancellation);
    }

    [Fact]
    public async Task BeginAsync_WhenSelectorThrows_FailsAuthenticationWithAFixedMessageAndTheCause()
    {
        var fault = new InvalidOperationException("selector boom");
        var descriptor = Bound();

        await using var context = await NativeProviderChatSend.BeginAsync(
            descriptor, Request(descriptor), new ThrowingSelector(fault), TimeProvider.System, TestContext.Current.CancellationToken);

        var failure = context.Failure.ShouldNotBeNull();
        failure.Kind.ShouldBe(ProviderFailureKind.Authentication);
        failure.SafeMessage.ShouldBe("The credential profile could not be selected.");
        failure.DiagnosticCause.ShouldBeSameAs(fault);
    }

    [Fact]
    public async Task BeginAsync_WhenSelectorTimesOutOnItsOwn_FailsWithTimeout()
    {
        var descriptor = Bound();

        await using var context = await NativeProviderChatSend.BeginAsync(
            descriptor, Request(descriptor), new ThrowingSelector(new OperationCanceledException()), TimeProvider.System, TestContext.Current.CancellationToken);

        context.Failure.ShouldNotBeNull().Kind.ShouldBe(ProviderFailureKind.Timeout);
    }

    [Fact]
    public async Task BeginAsync_WhenOperationContextIsMissing_FailsAuthorization()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")));
        var descriptor = Bound();
        var request = Request(descriptor) with { Operation = null };

        await using var context = await NativeProviderChatSend.BeginAsync(
            descriptor, request, selector, TimeProvider.System, TestContext.Current.CancellationToken);

        context.Failure.ShouldNotBeNull().Kind.ShouldBe(ProviderFailureKind.Authorization);
        selector.Selections.ShouldBe(0);
    }

    [Fact]
    public async Task BeginAsync_WhenArgumentIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("s")));
        var descriptor = Bound();
        var request = Request(descriptor);

        var d = await Should.ThrowAsync<ArgumentNullException>(async () => await NativeProviderChatSend.BeginAsync(null!, request, selector, TimeProvider.System, default));
        var r = await Should.ThrowAsync<ArgumentNullException>(async () => await NativeProviderChatSend.BeginAsync(descriptor, null!, selector, TimeProvider.System, default));
        var s = await Should.ThrowAsync<ArgumentNullException>(async () => await NativeProviderChatSend.BeginAsync(descriptor, request, null!, TimeProvider.System, default));
        var t = await Should.ThrowAsync<ArgumentNullException>(async () => await NativeProviderChatSend.BeginAsync(descriptor, request, selector, null!, default));

        d.ParamName.ShouldBe("descriptor");
        r.ParamName.ShouldBe("request");
        s.ParamName.ShouldBe("profileSelector");
        t.ParamName.ShouldBe("timeProvider");
    }

    private sealed class ThrowingSelector(Exception exception): IProviderProfileRuntimeSelector
    {
        public ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
            ProviderOperationBinding binding,
            ProtectedSemanticOperationContext operation,
            CancellationToken cancellationToken = default) =>
            throw exception;
    }
}
