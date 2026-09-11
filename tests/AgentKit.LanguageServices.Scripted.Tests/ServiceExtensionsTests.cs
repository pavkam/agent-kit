// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.LanguageServices.Scripted.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    private static readonly LanguageQueryId _queryId = new(Guid.Parse("50000000-0000-0000-0000-000000000005"));
    [Fact]
    public void AddScriptedLanguageIntelligence_WhenCalled_RegistersReplaceableServiceAndIdentifierGenerator()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore, TestGrantStore>();
        _ = services.AddScriptedLanguageIntelligence(static _ =>
        {
        });
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ILanguageIntelligenceService>().ShouldBeOfType<ScriptedLanguageIntelligenceService>();
        provider.GetRequiredService<IIdentifierGenerator<LanguageQueryId>>().Create().Value.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task AddScriptedLanguageIntelligence_WhenIntentGeneratorIsHostSupplied_UsesTheReplacement()
    {
        var expectedId = new SecurityEnforcementIntentId(Guid.Parse("82000000-0000-0000-0000-000000000008"));
        var store = new TestGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(store);
        _ = services.AddSingleton<IIdentifierGenerator<SecurityEnforcementIntentId>>(new FixedSecurityEnforcementIntentIdGenerator(expectedId));
        _ = services.AddScriptedLanguageIntelligence(options => options.Scenarios.Add(new ScriptedLanguageScenario(_queryId, Success(LanguageQueryKind.Diagnostics), TimeSpan.Zero)));
        using var provider = services.BuildServiceProvider();
        var result = await provider.GetRequiredService<ILanguageIntelligenceService>().QueryAsync(Request(), TestContext.Current.CancellationToken);
        result.Status.ShouldBe(LanguageQueryStatus.Success);
        store.Intents.ShouldHaveSingleItem().Id.ShouldBe(expectedId);
    }

    private static LanguageQueryRequest Request(SecurityGrant? grant = null, int maximumResults = 10) => new(_queryId, LanguageQueryKind.Diagnostics, new FileSystemPath("src/a.cs"), null, null, maximumResults, TimeSpan.FromSeconds(1), grant ?? TestGrantStore.Grant());
    private static LanguageQueryResult Success(LanguageQueryKind kind) => new(LanguageQueryStatus.Success, kind, null, [], [], [], true, null);
    private sealed class FixedSecurityEnforcementIntentIdGenerator(SecurityEnforcementIntentId value): IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => value;
    }
}
