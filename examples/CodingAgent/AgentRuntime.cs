// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using AgentKit.Context;
using AgentKit.FileSystem;
using AgentKit.Loop;
using AgentKit.Output;
using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.Processes;
using AgentKit.Providers;
using AgentKit.Providers.OpenAI;
using AgentKit.Session;
using AgentKit.Session.InMemory;
using AgentKit.Tools;
using AgentKit.Tools.Command;
using AgentKit.Tools.Edit;
using AgentKit.Tools.Glob;
using AgentKit.Tools.Plan;
using AgentKit.Tools.Read;
using AgentKit.Tools.Search;
using AgentKit.Tools.Write;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Composes a real AgentKit turn loop against OpenAI with file and process tools.</summary>
/// <remarks>
/// This composes <c>AddConversationSession</c> from <c>AgentKit.Conversations</c>, which drives session
/// creation, message admission, and the agent-loop run in one <see cref="IConversationSession.SendAsync"/>
/// call, together with <c>AddStandaloneSecurityProfile</c> and <c>AgentToolsOptions.AllowAllRegisteredTools</c>,
/// which together remove almost every piece of composition boilerplate this example needed before those
/// library helpers existed. See the project README for exactly what each one replaces. It also decorates
/// <see cref="IToolInvoker"/> with <see cref="ApprovalGatedToolInvoker"/>, so a write, edit, or command call
/// asks the supplied <see cref="IApprovalPrompt"/> before it runs — the tool-authorization contract alone
/// cannot do this because it never sees a call's arguments.
/// </remarks>
internal static class AgentRuntime
{
    /// <summary>Composes the full runtime for one workspace root and returns its resolved conversation session.</summary>
    /// <param name="workspaceRoot">The absolute directory this agent's file and process tools are scoped to.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="modelId">The OpenAI model id to use.</param>
    /// <param name="approvals">Approves or denies each mutating tool call before it runs.</param>
    public static IConversationSession Create(string workspaceRoot, string apiKey, string modelId, IApprovalPrompt approvals)
    {
        var services = new ServiceCollection();

        var agentId = new AgentId(Guid.NewGuid());
        var securityProfileKey = new SecurityProfileKey("coding-agent-security");
        var definitionRevision = new AgentDefinitionRevision(1);
        var configurationVersion = new ConfigurationVersion(1);
        var authorityKey = new ComponentKey<ISecurityAuthority>("coding-agent-authority");

        _ = services.AddSingleton<ISecurityGrantStore>(new InMemorySecurityGrantStore(TimeProvider.System));
        _ = services.AddStandaloneSecurityProfile(
            agentId,
            definitionRevision,
            configurationVersion,
            securityProfileKey,
            authorityKey,
            configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
        _ = services.AddAllowAllSecurityPolicy();

        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();
        _ = services.AddInMemorySessionDirectory(new ComponentId("coding-agent.session"));

        _ = services.AddAgentContext();
        _ = services.AddAgentOutput();
        _ = services.AddAgentLoop();

        _ = services.AddReadTool();
        _ = services.AddWriteTool();
        _ = services.AddEditTool();
        _ = services.AddGlobTool();
        _ = services.AddSearchTool();
        _ = services.AddCommandTool(o => o.SandboxProfile = PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile);
        _ = services.AddPlanTool();
        _ = services.AddAgentTools(o => o.AllowAllRegisteredTools = true);
        _ = services.AddSingleton(approvals);
        _ = services.RemoveAll<IToolInvoker>();
        _ = services.AddSingleton<IToolInvoker>(provider => new ApprovalGatedToolInvoker(
            new DefaultToolInvoker(provider.GetRequiredService<IToolCatalog>(), provider.GetRequiredService<IToolAuthorizer>()),
            provider.GetRequiredService<IApprovalPrompt>()));

        _ = services.AddSandboxedFileSystem(workspaceRoot);
        _ = services.AddOperatingSystemProcesses(workspaceRoot, o => o.AllowedExecutablePaths.Add("/bin/sh"));

        _ = services.AddAgentProviders();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential(apiKey);
        var alias = new ModelAlias("assistant");
        var typedModelId = new ModelId(modelId);
        _ = services.AddOpenAILlmModel(alias, typedModelId);
        _ = services.AddModelDescriptors(
            new ModelDescriptorSourceId("coding-agent"),
            [
                new ModelDescriptor(
                    alias,
                    OpenAIProviderDefaults.ProviderId,
                    OpenAIProviderDefaults.ApiFamily,
                    typedModelId,
                    null,
                    OpenAIProviderDefaults.DefaultCapabilities,
                    OpenAIProviderDefaults.DefaultLimits,
                    null,
                    ExtensionData.Empty)
            ]);

        var sessionProfile = new SessionProfileSnapshot(
            new SessionProfileReference(new SessionProfileKey("coding-agent-session"), new SessionProfileVersion(1)),
            new ComponentKey<ISessionCoordinator>("coordinator"),
            new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
            new SessionStoreKey("agentkit.in-memory"),
            SessionStoreCapabilities.None,
            requiresDurableStore: false,
            requiresDistributedFencing: false,
            new SessionRetentionProfileKey("retention"),
            SessionBusyBehavior.Reject,
            maximumAppendEntries: 128,
            maximumPageSize: 256,
            verifySnapshotHashes: true,
            deleteOnDispose: false,
            new ContentHash("sha256:coding-agent-session-profile"));

        _ = services.AddConversationSession(o =>
        {
            o.AgentId = agentId;
            o.Identity = LocalIdentity();
            o.SecurityProfileKey = securityProfileKey;
            o.AgentDefinitionRevision = definitionRevision;
            o.ConfigurationVersion = configurationVersion;
            o.SessionProfile = sessionProfile;
            o.ModelSelectionPolicy = new ModelSelectionPolicy([alias]);
            o.Instructions.Add(SystemMessage(agentId, workspaceRoot));
            o.MaxTurns = 12;
            o.AttemptTimeout = TimeSpan.FromMinutes(3);
        });

        // Populates the model-facing tool list from whatever ITool instances the container resolves, without
        // an intermediate "probe" provider build: OptionsBuilder<T>.Configure<TDep> resolves TDep lazily, the
        // first time ConversationSessionOptions itself is materialized (when IConversationSession is resolved).
        _ = services.AddOptions<ConversationSessionOptions>()
            .Configure<IEnumerable<ITool>>(static (options, tools) =>
            {
                foreach (var definition in tools.Select(static tool => tool.Descriptor).ToLlmToolDefinitions())
                {
                    options.Tools.Add(definition);
                }
            });

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        return provider.GetRequiredService<IConversationSession>();
    }

    private static ExecutionIdentity LocalIdentity() => new(
        new TenantId("local"),
        new PrincipalId("cli-user"),
        ExecutionSubjectKind.Human,
        new AuthenticationEvidence(
            new AuthenticationEvidenceId("local-cli"),
            new IdentityIssuerId("coding-agent"),
            "local-terminal",
            DateTimeOffset.UtcNow,
            null,
            new AuthenticationEvidenceFingerprint(new ContentHash("sha256:local-cli-session"))),
        [],
        [],
        IdentityAssuranceLevel.Basic,
        new IdentityVersion(1));

    private static SystemMessage SystemMessage(AgentId agentId, string workspaceRoot) => new(
        new MessageId(Guid.NewGuid()),
        agentId,
        default,
        null,
        default,
        null,
        null,
        DateTimeOffset.UtcNow,
        MessageState.Complete,
        [new TextPart(SystemPrompt(workspaceRoot), TextSemantics.Plain, ExtensionData.Empty)],
        ExtensionData.Empty);

    private static string SystemPrompt(string workspaceRoot) =>
        $"""
        You are a careful coding agent working in the directory {workspaceRoot}.
        Use the read, write, edit, glob, search, and command tools to inspect and modify files.
        Every tool path argument (including a command's working directory) must be relative to the
        workspace root, without a leading slash. Omit the working directory to use the workspace root itself.
        Prefer the smallest correct change. Explain what you did after acting.
        glob's "pattern" and search's "path_pattern" are path globs supporting only *, ?, and ** — there is no
        way to exclude a path, and neither tool consults .gitignore or skips build output on its own. search's
        own "pattern" argument is different: it is the text to find inside file contents (a literal substring,
        or a .NET regular expression when "regex" is true — the default), never a path glob; for example, to
        find "TODO" comments under src/AgentKit.Loop, call search with pattern "TODO", regex false, and
        base_path "src/AgentKit.Loop" — "pattern" here is always a plain string, never a number, and every
        other search argument (regex, case_sensitive, include_hidden, the maximum_* bounds) is optional with a
        sensible default, so omit any you don't need rather than guessing a value.
        Critically, the path-glob argument (glob's "pattern", search's "path_pattern") is matched against each
        entry only after the traversal already visited it — putting a literal directory prefix inside it (like
        "src/AgentKit.Loop/**/*.cs") does NOT limit which directories get walked; it still walks the entire tree
        under "base_path" (the workspace root, by default) and only filters afterward, so bin/, obj/, and every
        other build or dependency directory anywhere in the tree still counts against the visited-entry /
        candidate-file limit and will exhaust it almost immediately in any real repository. The one argument
        that actually limits what gets walked is "base_path": always set it to the narrowest directory that
        could contain what you're looking for (a specific project folder, not the workspace root), and keep
        the path-glob relative to that base_path (for example base_path "src/AgentKit.Loop", pattern
        "**/*.cs" — not a pattern that embeds "src/AgentKit.Loop" itself). If you don't yet know which
        directory to scope to, glob "*" with a shallow maximum_depth at the workspace root first to see the
        top-level layout, then narrow base_path from there before doing anything recursive. If a glob or
        search call fails because "base_path" does not exist, that is not the same as a match-free search —
        never report it as "no occurrences" or "nothing found"; instead glob the parent directory (or the
        workspace root, with a shallow maximum_depth) to see the real layout, then retry with a base_path
        you have actually confirmed exists.
        Trust only the tool result you actually receive, for every tool, not only write/edit/command: a
        failure (denied, timed out, base_path missing, limit exceeded, or any other error status) is never
        equivalent to an empty or negative result, and must never be reported to the user as one. Write, edit,
        and command calls specifically require the user's live approval and can be denied — if one reports
        denial or failure, tell the user plainly that the action did not happen, never claim to have made a
        change that was denied or failed.
        For any request that takes more than two or three tool calls to finish, start by calling the todo
        tool with a "replace" action to write out the full list of steps. Then, as you work, call it again
        with "set_status" to mark each item in_progress before you start it and completed the moment it is
        done — one call per item, with that item's own "item_id" and "status" (set_status takes a single
        item, never an "items" list; only "replace" takes the full array). Keep the list current for the
        whole task; do not batch status updates until the end. Skip the todo tool entirely for a single
        quick lookup or a one-line edit.
        Every successful todo response — including the one from "replace" itself — returns the plan's
        current state, and that state carries a numeric revision number. Every "replace" or "set_status"
        call after the first one is a mutation and is rejected unless it also includes "expected_revision"
        set to the exact revision number the most recent todo response reported; re-read that response
        before writing the next call instead of guessing or omitting it.
        """;
}
