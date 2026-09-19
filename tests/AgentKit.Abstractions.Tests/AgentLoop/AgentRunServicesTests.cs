// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunServices behavior and contracts.</summary>
public sealed class AgentRunServicesTests
{
    [Fact]
    public void Constructor_WhenSessionIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(null!, Selector(), Context(), Tools(), Catalog(), Selector2(), Resolver(), Policy())).ParamName.ShouldBe("session");

    [Fact]
    public void Constructor_WhenSecurityProfileSelectorIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), null!, Context(), Tools(), Catalog(), Selector2(), Resolver(), Policy())).ParamName.ShouldBe("securityProfileSelector");

    [Fact]
    public void Constructor_WhenContextIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), Selector(), null!, Tools(), Catalog(), Selector2(), Resolver(), Policy())).ParamName.ShouldBe("context");

    [Fact]
    public void Constructor_WhenToolsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), Selector(), Context(), null!, Catalog(), Selector2(), Resolver(), Policy())).ParamName.ShouldBe("tools");

    [Fact]
    public void Constructor_WhenModelsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), Selector(), Context(), Tools(), null!, Selector2(), Resolver(), Policy())).ParamName.ShouldBe("models");

    [Fact]
    public void Constructor_WhenModelSelectorIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), Selector(), Context(), Tools(), Catalog(), null!, Resolver(), Policy())).ParamName.ShouldBe("modelSelector");

    [Fact]
    public void Constructor_WhenModelResolverIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), Selector(), Context(), Tools(), Catalog(), Selector2(), null!, Policy())).ParamName.ShouldBe("modelResolver");

    [Fact]
    public void Constructor_WhenContinuationPolicyIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new AgentRunServices(Session(), Selector(), Context(), Tools(), Catalog(), Selector2(), Resolver(), null!)).ParamName.ShouldBe("continuationPolicy");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var session = Session();
        var securityProfileSelector = Selector();
        var context = Context();
        var tools = Tools();
        var models = Catalog();
        var modelSelector = Selector2();
        var modelResolver = Resolver();
        var continuationPolicy = Policy();
        var services = new AgentRunServices(session, securityProfileSelector, context, tools, models, modelSelector, modelResolver, continuationPolicy);
        services.Session.ShouldBeSameAs(session);
        services.SecurityProfileSelector.ShouldBeSameAs(securityProfileSelector);
        services.Context.ShouldBeSameAs(context);
        services.Tools.ShouldBeSameAs(tools);
        services.Models.ShouldBeSameAs(models);
        services.ModelSelector.ShouldBeSameAs(modelSelector);
        services.ModelResolver.ShouldBeSameAs(modelResolver);
        services.ContinuationPolicy.ShouldBeSameAs(continuationPolicy);
        services.Output.ShouldBeNull();
        services.Compactor.ShouldBeNull();
        services.Budgets.ShouldBeNull();
        services.RunCoordinator.ShouldBeNull();
        services.Input.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenRunCoordinatorIsSupplied_RoundTripsProperty()
    {
        var runCoordinator = new FakeSessionRunCoordinator();
        var services = new AgentRunServices(
            Session(), Selector(), Context(), Tools(), Catalog(), Selector2(), Resolver(), Policy(),
            output: null, compactor: null, budgets: null, runCoordinator: runCoordinator);

        services.RunCoordinator.ShouldBeSameAs(runCoordinator);
    }

    [Fact]
    public void Constructor_WhenInputIsSupplied_RoundTripsProperty()
    {
        var input = new FakeInputCoordinator();
        var services = new AgentRunServices(
            Session(), Selector(), Context(), Tools(), Catalog(), Selector2(), Resolver(), Policy(),
            output: null, compactor: null, budgets: null, runCoordinator: null, input: input);

        services.Input.ShouldBeSameAs(input);
    }

    [Fact]
    public void Constructor_WhenInputIsOmitted_DefaultsToNull()
    {
        var services = new AgentRunServices(Session(), Selector(), Context(), Tools(), Catalog(), Selector2(), Resolver(), Policy());

        services.Input.ShouldBeNull();
    }

    private static FakeSessionCoordinator Session() => new();
    private static FakeSecurityProfileSelector Selector() => new();
    private static FakeContextAssembler Context() => new();
    private static FakeToolInvoker Tools() => new();
    private static FakeModelCatalog Catalog() => new();
    private static FakeModelSelector Selector2() => new();
    private static FakeLlmModelResolver Resolver() => new();
    private static FakeContinuationPolicy Policy() => new();

    private sealed class FakeSessionCoordinator: ISessionCoordinator
    {
        public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSecurityProfileSelector: ISecurityProfileSelector
    {
        public ValueTask<SecurityAuthorizationCaptureResult> SelectAsync(SecurityAuthorizationCaptureRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeContextAssembler: IContextAssembler
    {
        public Task<ContextAssemblyResult> AssembleAsync(ContextAssemblyRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeToolInvoker: IToolInvoker
    {
        public Task<ResolvedToolInvocation> InvokeAsync(ToolCallRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeModelCatalog: IModelCatalog
    {
        public ValueTask<ModelCatalogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeModelSelector: IModelSelector
    {
        public ValueTask<ModelSelectionResult> SelectAsync(ModelSelectionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeLlmModelResolver: ILlmModelResolver
    {
        public ILlmModel? Resolve(ModelDescriptor model) => throw new NotSupportedException();
    }

    private sealed class FakeContinuationPolicy: IRunContinuationPolicy
    {
        public ValueTask<RunContinuationDecision> DecideAsync(RunContinuationContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeSessionRunCoordinator: ISessionRunCoordinator
    {
        public ValueTask<SessionRunLeaseResult> AcquireAsync(SessionRunLeaseRequest request, SessionExecutionCapability session, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeInputCoordinator: IInputCoordinator
    {
        public ValueTask<InputAdmissionResult> AdmitAsync(InputAdmissionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<InputPromotionResult> PromoteAsync(InputPromotionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
