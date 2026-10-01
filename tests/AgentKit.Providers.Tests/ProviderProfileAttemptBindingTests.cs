// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Tests;

using AgentKit.TestSupport;

/// <summary>Verifies that <see cref="ProviderProfileAttemptBinding"/> selects a runtime without ever resolving a credential.</summary>
public sealed class ProviderProfileAttemptBindingTests
{
    private static readonly ProviderId Provider = new("test-provider");

    [Fact]
    public async Task SelectRuntimeAsync_WhenOperationIsUnbound_SelectsNothingAndSucceeds()
    {
        var source = new StaticProviderCredentialSource(new ApiKeyProviderCredential("secret"));
        var selector = new StaticProviderProfileRuntimeSelector(source);

        var selection = await ProviderProfileAttemptBinding.SelectRuntimeAsync(
            binding: null, ProviderEgressHarness.Operation, selector, Provider, TestContext.Current.CancellationToken);

        selection.IsSuccess.ShouldBeTrue();
        selection.Runtime.ShouldBeNull();
        selection.EndpointBaseAddress.ShouldBeNull();
        selector.Selections.ShouldBe(0);
    }

    [Fact]
    public async Task SelectRuntimeAsync_WhenBound_ReturnsTheRuntimeAndItsEndpointWithoutReadingTheCredential()
    {
        var source = new StaticProviderCredentialSource(new ApiKeyProviderCredential("secret"));
        var selector = new StaticProviderProfileRuntimeSelector(source, new Uri("https://selected.test/api/"));

        var selection = await ProviderProfileAttemptBinding.SelectRuntimeAsync(
            StaticProviderProfileRuntimeSelector.Binding, ProviderEgressHarness.Operation, selector, Provider, TestContext.Current.CancellationToken);

        selection.IsSuccess.ShouldBeTrue();
        selection.Runtime.ShouldNotBeNull().CredentialSource.ShouldBeSameAs(source);
        selection.EndpointBaseAddress.ShouldBe(new Uri("https://selected.test/api/"));
        source.ResolutionCount.ShouldBe(0);
    }

    [Fact]
    public async Task SelectRuntimeAsync_WhenBoundWithoutOperationContext_FailsAuthorizationWithoutSelecting()
    {
        var selector = new StaticProviderProfileRuntimeSelector(new StaticProviderCredentialSource(new ApiKeyProviderCredential("secret")));

        var selection = await ProviderProfileAttemptBinding.SelectRuntimeAsync(
            StaticProviderProfileRuntimeSelector.Binding, operation: null, selector, Provider, TestContext.Current.CancellationToken);

        selection.IsSuccess.ShouldBeFalse();
        selection.Failure.ShouldNotBeNull().Kind.ShouldBe(ProviderFailureKind.Authorization);
        selection.Runtime.ShouldBeNull();
        selector.Selections.ShouldBe(0);
    }

    [Fact]
    public async Task SelectRuntimeAsync_WhenSelectorIsUnavailable_ReturnsTheSelectorFailure()
    {
        var selector = new UnavailableSelector();

        var selection = await ProviderProfileAttemptBinding.SelectRuntimeAsync(
            StaticProviderProfileRuntimeSelector.Binding, ProviderEgressHarness.Operation, selector, Provider, TestContext.Current.CancellationToken);

        selection.Failure.ShouldNotBeNull().SafeMessage.ShouldBe("not registered");
        selection.Runtime.ShouldBeNull();
    }

    [Fact]
    public async Task SelectRuntimeAsync_WhenSelectorIsNull_ThrowsArgumentNullException()
    {
        var exception = await Should.ThrowAsync<ArgumentNullException>(async () => await ProviderProfileAttemptBinding.SelectRuntimeAsync(
            null, null, null!, Provider, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("profileSelector");
    }

    [Fact]
    public void NormalizeBaseAddress_WhenWithoutTrailingSlash_AppendsOne()
    {
        ProviderProfileAttemptBinding.NormalizeBaseAddress(new Uri("https://x.test/api")).ShouldBe(new Uri("https://x.test/api/"));
        ProviderProfileAttemptBinding.NormalizeBaseAddress(new Uri("https://x.test/api/")).ShouldBe(new Uri("https://x.test/api/"));
        Should.Throw<ArgumentNullException>(() => ProviderProfileAttemptBinding.NormalizeBaseAddress(null!)).ParamName.ShouldBe("baseAddress");
    }

    [Fact]
    public void ProfileRuntimeSelection_WhenFactoriesReceiveNull_ThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ProviderProfileAttemptBinding.ProfileRuntimeSelection.FromRuntime(null!)).ParamName.ShouldBe("runtime");
        Should.Throw<ArgumentNullException>(() => ProviderProfileAttemptBinding.ProfileRuntimeSelection.FromFailure(null!)).ParamName.ShouldBe("failure");
    }

    private sealed class UnavailableSelector: IProviderProfileRuntimeSelector
    {
        public ValueTask<ProviderProfileRuntimeSelectionResult> SelectAsync(
            ProviderOperationBinding binding,
            ProtectedSemanticOperationContext operation,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProviderProfileRuntimeSelectionResult>(new ProviderProfileRuntimeUnavailable(
                binding,
                new ProviderFailure(ProviderFailureKind.InvalidRequest, Provider, null, null, null, null, "not registered", null, ExtensionData.Empty)));
    }
}
