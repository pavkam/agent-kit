// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Language.Tests;

public sealed class LanguageToolTests
{
    [Theory]
    [InlineData(/*lang=json,strict*/ "{}")]
    [InlineData(/*lang=json,strict*/ "{\"action\":\"hover\",\"path\":\"a.cs\"}")]
    [InlineData(/*lang=json,strict*/ "{\"action\":\"diagnostics\",\"path\":\"a.cs\",\"line\":1,\"character\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"action\":\"workspace_symbols\",\"query\":\"\"}")]
    [InlineData(/*lang=json,strict*/ "{\"action\":\"references\",\"path\":\"../a.cs\",\"line\":1,\"character\":1}")]
    [InlineData(/*lang=json,strict*/ "{\"action\":\"definitions\",\"path\":\"a.cs\",\"line\":0,\"character\":1}")]
    public async Task InvokeAsync_WhenArgumentsInvalid_PerformsNoAuthorizationOrQuery(string json)
    {
        var service = new RecordingLanguageService();
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(service, authority).InvokeAsync(
            Request(json), TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        authority.Requests.ShouldBeEmpty();
        service.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_WhenAuthorityDenies_PerformsNoQuery()
    {
        var service = new RecordingLanguageService();

        var result = await CreateTool(service, new RecordingSecurityAuthority(allow: false)).InvokeAsync(
            Request(/*lang=json,strict*/ """{"action":"diagnostics","path":"src/a.cs"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.FailureReason.ShouldBe("Denied.");
        service.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("diagnostics", LanguageQueryKind.Diagnostics, false)]
    [InlineData("hover", LanguageQueryKind.Hover, true)]
    [InlineData("definitions", LanguageQueryKind.Definitions, true)]
    [InlineData("implementations", LanguageQueryKind.Implementations, true)]
    [InlineData("references", LanguageQueryKind.References, true)]
    [InlineData("document_symbols", LanguageQueryKind.DocumentSymbols, false)]
    public async Task InvokeAsync_WhenDocumentActionValid_UsesExactSecurityEvidence(
        string action,
        LanguageQueryKind kind,
        bool positionRequired)
    {
        var service = new RecordingLanguageService
        {
            Result = Success(kind),
        };
        var authority = new RecordingSecurityAuthority();
        var position = positionRequired ? ",\"line\":2,\"character\":3" : string.Empty;

        var result = await CreateTool(service, authority).InvokeAsync(
            Request($$"""{"action":"{{action}}","path":"src/a.cs"{{position}},"maximum_results":7,"timeout_ms":250}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var query = service.Requests.ShouldHaveSingleItem();
        query.Kind.ShouldBe(kind);
        query.Path.ShouldBe(new FileSystemPath("src/a.cs"));
        query.Position.ShouldBe(positionRequired ? new LanguagePosition(1, 2) : null);
        query.MaximumResults.ShouldBe(7);
        query.Timeout.ShouldBe(TimeSpan.FromMilliseconds(250));
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Audience.ShouldBe(service.SecurityAudience);
        security.Kind.ShouldBe(SecurityOperationKind.FileRead);
        security.Effect.ShouldBe(SecurityEffect.Observe);
        security.Resources.ShouldBe([LanguageSecurityBinding.Resource(kind, query.Path)]);
        security.InputFingerprint.ShouldBe(LanguageSecurityBinding.Fingerprint(query));
        query.Grant.RequestId.ShouldBe(security.Id);
    }

    [Fact]
    public async Task InvokeAsync_WhenWorkspaceSymbolQueryValid_OmitsPathAndHashesQueryInEvidence()
    {
        var service = new RecordingLanguageService { Result = Success(LanguageQueryKind.WorkspaceSymbols) };
        var authority = new RecordingSecurityAuthority();

        var result = await CreateTool(service, authority).InvokeAsync(
            Request(/*lang=json,strict*/ """{"action":"workspace_symbols","query":"SecretName"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        var query = service.Requests.ShouldHaveSingleItem();
        query.Query.ShouldBe("SecretName");
        query.Path.ShouldBeNull();
        var security = authority.Requests.ShouldHaveSingleItem();
        security.Resources.ShouldBe([new ProtectedResource(ProtectedResourceKind.Directory, ".")]);
        security.InputFingerprint.Value.ShouldNotContain("SecretName");
        security.InputFingerprint.ShouldBe(LanguageSecurityBinding.Fingerprint(query));
    }

    [Fact]
    public async Task InvokeAsync_WhenProviderReturnsOversizedSnapshot_ProjectsBoundedLossAwareOutput()
    {
        var location = new LanguageLocation(
            new FileSystemPath("src/very-long-name.cs"),
            new LanguageRange(new LanguagePosition(0, 1), new LanguagePosition(2, 3)),
            new ContentHash("sha256:document"));
        var service = new RecordingLanguageService
        {
            Result = new LanguageQueryResult(
                LanguageQueryStatus.Success,
                LanguageQueryKind.Diagnostics,
                "oversized hover",
                [location, location],
                [],
                [
                    new LanguageDiagnostic(LanguageDiagnosticSeverity.Error, "message-long", "code-long", "source-long", location),
                    new LanguageDiagnostic(LanguageDiagnosticSeverity.Warning, "second", null, null, location),
                ],
                true,
                null),
        };
        var options = OptionsForTool();
        options.MaximumTextCharacters = 7;

        var result = await CreateTool(service, new RecordingSecurityAuthority(), options).InvokeAsync(
            Request(/*lang=json,strict*/ """{"action":"diagnostics","path":"src/a.cs","maximum_results":1}"""),
            TestContext.Current.CancellationToken);

        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("complete").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("projection_truncated").GetBoolean().ShouldBeTrue();
        json.RootElement.GetProperty("locations").GetArrayLength().ShouldBe(1);
        json.RootElement.GetProperty("diagnostics").GetArrayLength().ShouldBe(1);
        json.RootElement.GetProperty("hover").GetString().ShouldBe("oversiz");
        json.RootElement.GetProperty("diagnostics")[0].GetProperty("message").GetString().ShouldBe("message");
        json.RootElement.GetProperty("locations")[0].GetProperty("start_line").GetInt32().ShouldBe(1);
        json.RootElement.GetProperty("locations")[0].GetProperty("start_character").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task InvokeAsync_WhenProviderReportsStale_ReturnsFailureWithTypedSnapshot()
    {
        var service = new RecordingLanguageService
        {
            Result = new LanguageQueryResult(
                LanguageQueryStatus.Stale,
                LanguageQueryKind.Diagnostics,
                null,
                [],
                [],
                [],
                false,
                "Document changed."),
        };

        var result = await CreateTool(service, new RecordingSecurityAuthority()).InvokeAsync(
            Request(/*lang=json,strict*/ """{"action":"diagnostics","path":"src/a.cs"}"""),
            TestContext.Current.CancellationToken);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Failed);
        result.Outcome.FailureReason.ShouldBe("Document changed.");
        using var json = JsonDocument.Parse(result.Content.ShouldHaveSingleItem().ShouldBeOfType<TextPart>().Text);
        json.RootElement.GetProperty("status").GetString().ShouldBe("Stale");
    }

    private static LanguageTool CreateTool(
        ILanguageIntelligenceService service,
        ISecurityAuthority authority,
        LanguageToolOptions? options = null) => new(
            service,
            authority,
            new FixedSecurityRequestIdGenerator(),
            new FixedLanguageQueryIdGenerator(),
            new FixedTimeProvider(),
            Options.Create(options ?? OptionsForTool()));

    private static LanguageToolOptions OptionsForTool() => new()
    {
        DefaultMaximumResults = 10,
        MaximumResults = 100,
        DefaultTimeout = TimeSpan.FromSeconds(2),
        MaximumTimeout = TimeSpan.FromSeconds(5),
        MaximumQueryCharacters = 100,
        MaximumTextCharacters = 100,
    };

    private static LanguageQueryResult Success(LanguageQueryKind kind) => new(
        LanguageQueryStatus.Success,
        kind,
        null,
        [],
        [],
        [],
        true,
        null);

    private static ToolInvocationRequest Request(string json) => new(
        TestSupport.TestSecurityEvidence.ToolContext(
            new AgentId(Guid.Parse("40000000-0000-0000-0000-000000000004")),
            new SessionId(Guid.Parse("50000000-0000-0000-0000-000000000005")),
            new ToolCallId(Guid.Parse("60000000-0000-0000-0000-000000000006")),
            new InRunOperationCorrelation(
                new OperationId(Guid.Parse("70000000-0000-0000-0000-000000000007")),
                new RunId(Guid.Parse("80000000-0000-0000-0000-000000000008")),
                null),
            TestSupport.TestExecutionIdentity.Create(
                new TenantId("tenant"),
                new PrincipalId("principal"),
                ExecutionSubjectKind.Human)),
        JsonDocument.Parse(json).RootElement,
        DateTimeOffset.UnixEpoch);
}
