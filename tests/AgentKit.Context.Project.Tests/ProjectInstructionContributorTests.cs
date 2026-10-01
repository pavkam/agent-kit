// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Project.Tests;

using Microsoft.Extensions.Options;

/// <summary>Tests for <see cref="ProjectInstructionContributor"/>.</summary>
public sealed class ProjectInstructionContributorTests
{
    private static readonly FileSystemProfileKey _profileKey = new("workspace");

    /// <summary>Verifies a readable instruction file becomes one workspace-trust instruction candidate.</summary>
    [Fact]
    public async Task ContributeAsync_WhenAgentsFileExists_ReturnsInstructionCandidate()
    {
        var reader = new FakeFileReader();
        reader.SeedText("AGENTS.md", "project rules");
        var contributor = CreateContributor(reader);
        var request = CreateContributionRequest();

        var contribution = await contributor.ContributeAsync(request, TestContext.Current.CancellationToken);

        contribution.Candidates.Length.ShouldBe(1);
        contribution.Candidates[0].Kind.ShouldBe(ContextCandidateKind.Instruction);
        contribution.Candidates[0].Trust.ShouldBe(ContextTrust.Workspace);
        ((TextPart) contribution.Candidates[0].Content[0]).Text.ShouldBe("project rules");
    }

    /// <summary>Verifies the read is bound to the configured root, the discovered path, the byte bound, and the reader audience.</summary>
    [Fact]
    public async Task ContributeAsync_WhenReadIsAuthorized_BindsRootPathBoundAndGrantToTheReader()
    {
        var reader = new FakeFileReader();
        reader.SeedText("docs/CLAUDE.md", "nested rules");
        var contributor = CreateContributor(
            reader,
            new ProjectInstructionOptions
            {
                HostRootPath = HostRoot,
                RootId = new FileRootId("repo"),
                SearchRoots = ["docs/"],
                InstructionFilenames = ["CLAUDE.md"],
                MaxBytesPerFile = 1_024,
            });
        var request = CreateContributionRequest();

        _ = await contributor.ContributeAsync(request, TestContext.Current.CancellationToken);

        var read = reader.OpenedReads.ShouldHaveSingleItem();
        read.Request.Target.RootId.ShouldBe(new FileRootId("repo"));
        read.Request.Target.Path.Value.ShouldBe("docs/CLAUDE.md");
        read.Request.Bounds.MaxBytes.ShouldBe(1_024);
        read.Request.AgentId.ShouldBe(request.Agent.Id);
        read.Request.RunId.ShouldBe(request.RunId);
        read.ResolvedTarget.HostTargetPath.ShouldBe(Path.GetFullPath(Path.Combine(HostRoot, "docs", "CLAUDE.md")));
        read.Grant.Audience.ShouldBe(reader.SecurityAudience);
        read.Grant.Kind.ShouldBe(SecurityOperationKind.FileRead);
    }

    /// <summary>Verifies absent instruction files contribute nothing and do not fail discovery.</summary>
    [Fact]
    public async Task ContributeAsync_WhenNoInstructionFileExists_ReturnsNoCandidates()
    {
        var contributor = CreateContributor(new FakeFileReader());

        var contribution = await contributor.ContributeAsync(CreateContributionRequest(), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
    }

    /// <summary>Verifies discovery visits every search root and filename in deterministic order.</summary>
    [Fact]
    public async Task ContributeAsync_WhenSeveralFilesExist_ReturnsCandidatesInRootThenFilenameOrder()
    {
        var reader = new FakeFileReader();
        reader.SeedText("CLAUDE.md", "root claude");
        reader.SeedText("sub/AGENTS.md", "sub agents");
        reader.SeedText("AGENTS.md", "root agents");
        var contributor = CreateContributor(
            reader,
            new ProjectInstructionOptions { HostRootPath = HostRoot, SearchRoots = [".", "sub"] });

        var contribution = await contributor.ContributeAsync(CreateContributionRequest(), TestContext.Current.CancellationToken);

        contribution.Candidates
            .Select(static candidate => ((TextPart) candidate.Content[0]).Text)
            .ShouldBe(["root agents", "root claude", "sub agents"]);
        reader.OpenedReads.Select(static read => read.Request.Target.Path.Value)
            .ShouldBe(["AGENTS.md", "CLAUDE.md", "sub/AGENTS.md", "sub/CLAUDE.md"]);
    }

    /// <summary>Verifies a denied authorization never reaches the reader.</summary>
    [Fact]
    public async Task ContributeAsync_WhenAuthorityDenies_DoesNotOpenAnyFile()
    {
        var reader = new FakeFileReader();
        reader.SeedText("AGENTS.md", "project rules");
        var contributor = CreateContributor(reader, authority: new DenyingSecurityAuthority());

        var contribution = await contributor.ContributeAsync(CreateContributionRequest(), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
        reader.OpenedReads.ShouldBeEmpty();
    }

    /// <summary>Verifies a file longer than the configured bound is skipped rather than truncated.</summary>
    [Fact]
    public async Task ContributeAsync_WhenFileExceedsMaximumBytes_SkipsIt()
    {
        var reader = new FakeFileReader();
        reader.SeedText("AGENTS.md", new string('x', 33));
        var contributor = CreateContributor(
            reader,
            new ProjectInstructionOptions { HostRootPath = HostRoot, MaxBytesPerFile = 32 });

        var contribution = await contributor.ContributeAsync(CreateContributionRequest(), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
    }

    /// <summary>Verifies a file that is not valid UTF-8 contributes nothing.</summary>
    [Fact]
    public async Task ContributeAsync_WhenFileIsNotValidUtf8_SkipsIt()
    {
        var reader = new FakeFileReader();
        reader.SeedBytes("AGENTS.md", [0xC3, 0x28]);
        var contributor = CreateContributor(reader);

        var contribution = await contributor.ContributeAsync(CreateContributionRequest(), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
    }

    /// <summary>Verifies an unregistered profile yields no candidates and no read.</summary>
    [Fact]
    public async Task ContributeAsync_WhenProfileIsNotRegistered_ReturnsNoCandidates()
    {
        var reader = new FakeFileReader();
        reader.SeedText("AGENTS.md", "project rules");
        var contributor = CreateContributor(
            reader,
            new ProjectInstructionOptions { HostRootPath = HostRoot, ProfileKey = new FileSystemProfileKey("elsewhere") });

        var contribution = await contributor.ContributeAsync(CreateContributionRequest(), TestContext.Current.CancellationToken);

        contribution.Candidates.ShouldBeEmpty();
        reader.OpenedReads.ShouldBeEmpty();
    }

    /// <summary>Verifies cancellation propagates before any read.</summary>
    [Fact]
    public async Task ContributeAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var reader = new FakeFileReader();
        var contributor = CreateContributor(reader);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(
            async () => await contributor.ContributeAsync(CreateContributionRequest(), cancellation.Token));

        reader.OpenedReads.ShouldBeEmpty();
    }

    /// <summary>Verifies a null request is rejected before any effect.</summary>
    [Fact]
    public async Task ContributeAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        var reader = new FakeFileReader();
        var contributor = CreateContributor(reader);

        var exception = await Should.ThrowAsync<ArgumentNullException>(
            async () => await contributor.ContributeAsync(null!, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("request");
        reader.OpenedReads.ShouldBeEmpty();
    }

    /// <summary>Verifies each required constructor dependency is null-checked by name.</summary>
    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsArgumentNullExceptionNamingIt()
    {
        var selector = new FakeFileSystemSelector(_profileKey, new FakeFileReader());
        var authority = new FixedSecurityAuthoritySelector(new AllowAllSecurityAuthority(new InMemoryGrantStore()));
        var options = Options.Create(new ProjectInstructionOptions { HostRootPath = HostRoot });

        Should.Throw<ArgumentNullException>(() => new ProjectInstructionContributor(null!, authority, new GuidSecurityRequestIdGenerator(), new GuidFileOperationIdGenerator(), TimeProvider.System, options))
            .ParamName.ShouldBe("fileSystemSelector");
        Should.Throw<ArgumentNullException>(() => new ProjectInstructionContributor(selector, null!, new GuidSecurityRequestIdGenerator(), new GuidFileOperationIdGenerator(), TimeProvider.System, options))
            .ParamName.ShouldBe("authoritySelector");
        Should.Throw<ArgumentNullException>(() => new ProjectInstructionContributor(selector, authority, null!, new GuidFileOperationIdGenerator(), TimeProvider.System, options))
            .ParamName.ShouldBe("requestIds");
        Should.Throw<ArgumentNullException>(() => new ProjectInstructionContributor(selector, authority, new GuidSecurityRequestIdGenerator(), null!, TimeProvider.System, options))
            .ParamName.ShouldBe("fileOperationIds");
        Should.Throw<ArgumentNullException>(() => new ProjectInstructionContributor(selector, authority, new GuidSecurityRequestIdGenerator(), new GuidFileOperationIdGenerator(), null!, options))
            .ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new ProjectInstructionContributor(selector, authority, new GuidSecurityRequestIdGenerator(), new GuidFileOperationIdGenerator(), TimeProvider.System, null!))
            .ParamName.ShouldBe("options");
    }

    /// <summary>Verifies invalid option values are rejected at composition rather than during a run.</summary>
    [Fact]
    public void Constructor_WhenOptionsAreInvalid_FailsAtCompositionWithTheOffendingParameter()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CreateContributor(new FakeFileReader(), new ProjectInstructionOptions { HostRootPath = HostRoot, MaxBytesPerFile = 0 }))
            .ParamName.ShouldBe("options");
        Should.Throw<ArgumentException>(() => CreateContributor(new FakeFileReader(), new ProjectInstructionOptions { HostRootPath = " " }))
            .ParamName.ShouldBe("options");
        _ = Should.Throw<ArgumentException>(() => CreateContributor(new FakeFileReader(), new ProjectInstructionOptions { HostRootPath = HostRoot, SearchRoots = [" "] }));
        _ = Should.Throw<ArgumentException>(() => CreateContributor(new FakeFileReader(), new ProjectInstructionOptions { HostRootPath = HostRoot, InstructionFilenames = [""] }));
        _ = Should.Throw<ArgumentException>(() => CreateContributor(new FakeFileReader(), new ProjectInstructionOptions { HostRootPath = HostRoot, SearchRoots = ["../escape"] }));
    }

    private static string HostRoot { get; } = Path.Combine(Path.GetTempPath(), "agentkit-project-instruction-tests");

    private static ProjectInstructionContributor CreateContributor(
        FakeFileReader reader,
        ProjectInstructionOptions? options = null,
        ISecurityAuthority? authority = null) => new(
        new FakeFileSystemSelector(_profileKey, reader),
        new FixedSecurityAuthoritySelector(authority ?? new AllowAllSecurityAuthority(new InMemoryGrantStore())),
        new GuidSecurityRequestIdGenerator(),
        new GuidFileOperationIdGenerator(),
        TimeProvider.System,
        Options.Create(options ?? new ProjectInstructionOptions { HostRootPath = HostRoot }));

    private static ContextContributionRequest CreateContributionRequest()
    {
        var agentId = new AgentId(Guid.NewGuid());
        var sessionId = new SessionId(Guid.NewGuid());
        var branchId = new BranchId(Guid.NewGuid());
        var runId = new RunId(Guid.NewGuid());
        var turnId = new TurnId(Guid.NewGuid());
        var agent = AgentDefinitionFixtures.Create(agentId, revision: 1, displayName: "agent");
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var correlation = new InRunOperationCorrelation(new OperationId(Guid.NewGuid()), runId, turnId);
        var authorization = new SecurityAuthorizationContext(
            new SecurityProfileKey("security"),
            new SecurityProfileVersion(1),
            new SecurityPolicySnapshotReference(
                new SecurityPolicySnapshotId(Guid.NewGuid()),
                new SecurityPolicyVersion(1),
                new ContentHash("sha256:policy")),
            new ComponentKey<ISecurityAuthority>("authority"),
            new AgentDefinitionRevision(1),
            new ConfigurationVersion(1),
            new SecurityAuthorizationScope(agentId, sessionId, correlation),
            identity);
        var user = new UserMessage(
            new MessageId(Guid.NewGuid()),
            agentId,
            sessionId,
            null,
            branchId,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            MessageState.Complete,
            [new TextPart("hello", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var history = new HistoryView(
            new MessageCursor(agentId, sessionId, null, branchId, new SessionVersion(1), new SessionSequence(0)),
            [user],
            []);
        var configuration = new EffectiveConfigurationSnapshot(
            new ConfigurationVersion(1),
            new ContentHash("sha256:configuration"),
            [],
            []);
        return new ContextContributionRequest(
            agent,
            sessionId,
            conversationId: null,
            identity,
            runId,
            turnId,
            new ModelRequestId(Guid.NewGuid()),
            new ModelDescriptor(
                new ModelAlias("chat"),
                new ProviderId("test"),
                new ApiFamilyId("test"),
                new ModelId("test"),
                null,
                new ModelCapabilities(true, true, true, true, true, true, true, ExtensionData.Empty),
                new ModelLimits(4096, 1024),
                null,
                ExtensionData.Empty),
            history,
            authorization,
            configuration);
    }

    private sealed class DenyingSecurityAuthority: ISecurityAuthority
    {
        public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return ValueTask.FromResult<SecurityDecision>(new SecurityDenied(
                request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied.")));
        }
    }

    private sealed class InMemoryGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant,
            SecurityEnforcementRequest enforcement,
            SecurityEnforcementIntent intent,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(
                GrantConsumptionStatus.Consumed,
                0,
                "Consumed.",
                new SecurityEnforcementIntentReceipt(
                    intent.Id,
                    grant.Id,
                    grant.RequestId,
                    enforcement,
                    intent.RequiredFence,
                    SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                    DateTimeOffset.UnixEpoch)));

        public ValueTask<GrantRevocationResult> RevokeAsync(
            GrantId grantId,
            RevocationReason reason,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }
}
