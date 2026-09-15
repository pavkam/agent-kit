// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace CodingAgent;

using System.Security.Cryptography;
using System.Text;

using AgentKit.Context;
using AgentKit.FileSystem;
using AgentKit.IO;
using AgentKit.Loop;
using AgentKit.Output;
using AgentKit.Permissions;
using AgentKit.Permissions.InMemory;
using AgentKit.Processes;
using AgentKit.Providers;
using AgentKit.Providers.OpenAI;
using AgentKit.Session;
using AgentKit.Session.Sqlite;
using AgentKit.Tools;
using AgentKit.Tools.Command;
using AgentKit.Tools.Edit;
using AgentKit.Tools.Glob;
using AgentKit.Tools.Plan;
using AgentKit.Tools.Question;
using AgentKit.Tools.Read;
using AgentKit.Tools.Search;
using AgentKit.Tools.Write;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Composes a real AgentKit turn loop against OpenAI with file and process tools.</summary>
/// <remarks>
/// This composes <c>AddConversationSession</c> from <c>AgentKit.Conversations</c>, which drives session
/// creation, message admission, and the agent-loop run in one conversation <c>SendAsync</c>
/// call, together with <c>AddStandaloneSecurityProfile</c> and <c>AgentToolsOptions.AllowAllRegisteredTools</c>,
/// which together remove almost every piece of composition boilerplate this example needed before those
/// library helpers existed. See the project README for exactly what each one replaces. Permission modes
/// contribute normalized security policy, while the approval broker retains and validates the exact request
/// before the terminal UI can approve it.
/// </remarks>
internal static class AgentRuntime
{
    private static readonly SqliteSessionStoreInstanceId _sessionStoreInstanceId = new(
        Guid.Parse("ca000000-0000-0000-0000-000000000002"));

    /// <summary>Composes the full runtime for one workspace root and returns its resolved conversation session.</summary>
    /// <param name="workspaceRoot">The absolute directory this agent's file and process tools are scoped to.</param>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="configuration">The model, reasoning, turn, and external-root choices to capture.</param>
    /// <param name="approvals">Presents approval interactions in the authenticated terminal.</param>
    /// <param name="questions">Presents human questions in the authenticated terminal.</param>
    /// <param name="permissions">Provides the current UI mode to the normalized security policy.</param>
    public static OwnedConversationSession Create(
        string workspaceRoot,
        string apiKey,
        CodingAgentConfiguration configuration,
        IApprovalPrompt approvals,
        IHumanQuestionPrompt questions,
        PermissionModeController permissions)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var services = new ServiceCollection();
        var toolchainRoots = configuration.ReadOnlyToolchainRoots;

        var agentId = AgentIdForWorkspace(workspaceRoot);
        var securityProfileKey = new SecurityProfileKey("coding-agent-security");
        var definitionRevision = new AgentDefinitionRevision(1);
        var configurationVersion = new ConfigurationVersion(1);
        var authorityKey = new ComponentKey<ISecurityAuthority>("coding-agent-authority");
        var identity = LocalIdentity();

        _ = services.AddInMemorySecurityGrantStore();
        _ = services.AddInMemoryApprovalStore();
        _ = services.AddStandaloneSecurityProfile(
            agentId,
            definitionRevision,
            configurationVersion,
            securityProfileKey,
            authorityKey,
            configurePermissions: o => o.AuditDelivery = SecurityAuditDelivery.BestEffort);
        _ = services.AddSingleton<ISecurityPolicy>(new CodingAgentSecurityPolicy(permissions));
        _ = services.RemoveAll<IApprovalHandler>();
        _ = services.AddSingleton<IApprovalHandler>(new CodingAgentApprovalHandler(
            approvals,
            identity,
            TimeProvider.System));
        _ = services.RemoveAll<IApprovalResponderAuthorizer>();
        _ = services.AddSingleton<IApprovalResponderAuthorizer>(new CodingAgentApprovalResponderAuthorizer(identity));
        _ = services.AddSingleton<IHumanQuestionChannel>(new CodingAgentHumanQuestionChannel(
            questions,
            identity,
            TimeProvider.System));
        _ = services.AddHumanQuestionBroker();

        _ = services.AddAgentSession();
        var sessionDatabasePath = SessionDatabasePath(workspaceRoot);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(sessionDatabasePath)!);
        var sessionTarget = new SqliteSessionStoreTarget(
            sessionDatabasePath,
            _sessionStoreInstanceId,
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations);
        _ = services.AddSqliteSessionStore(sessionTarget);
        _ = services.AddSqliteSessionDirectory(new ComponentId("coding-agent.session"), sessionTarget);

        _ = services.AddAgentContext();
        _ = services.AddAgentOutput();
        _ = services.AddAgentLoop();

        _ = services.AddReadTool();
        _ = services.AddWriteTool();
        _ = services.AddEditTool();
        _ = services.AddGlobTool();
        _ = services.AddSearchTool();
        _ = services.AddCommandTool(o =>
        {
            o.SandboxProfile = PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile;
            o.ShellArguments.Clear();
            o.ShellArguments.Add("-c");
            o.EnvironmentVariables["PATH"] = CodingAgentHostEnvironment.CommandPath();
        });
        _ = services.AddPlanTool();
        _ = services.AddQuestionTool();
        _ = services.AddAgentTools(o => o.AllowAllRegisteredTools = true);

        _ = services.AddSandboxedFileSystem(workspaceRoot);
        _ = services.AddOperatingSystemProcesses(workspaceRoot, o =>
        {
            o.AllowedExecutablePaths.Add("/bin/sh");
            o.AllowedEnvironmentVariableNames.Add("PATH");
            for (var index = 0; index < toolchainRoots.Length; index++)
            {
                o.ReadOnlyToolchainRoots[$"toolchain-{index}"] = toolchainRoots[index];
            }
        });

        _ = services.AddAgentProviders();
        _ = services.AddOpenAI();
        _ = services.AddOpenAIApiKeyCredential(apiKey);
        var alias = new ModelAlias("assistant");
        var typedModelId = new ModelId(configuration.ModelId);
        var modelCapabilities = OpenAIProviderDefaults.DefaultCapabilities with { SupportsReasoning = true };
        _ = services.AddOpenAILlmModel(alias, typedModelId, modelCapabilities);
        _ = services.AddModelDescriptors(
            new ModelDescriptorSourceId("coding-agent"),
            [
                new ModelDescriptor(
                    alias,
                    OpenAIProviderDefaults.ProviderId,
                    OpenAIProviderDefaults.ApiFamily,
                    typedModelId,
                    null,
                    modelCapabilities,
                    OpenAIProviderDefaults.DefaultLimits,
                    null,
                    ExtensionData.Empty)
            ]);

        var sessionProfile = new SessionProfileSnapshot(
            new SessionProfileReference(new SessionProfileKey("coding-agent-session"), new SessionProfileVersion(1)),
            new ComponentKey<ISessionCoordinator>("coordinator"),
            new ComponentKey<ISessionRunCoordinator>("run-coordinator"),
            new SessionStoreKey("agentkit.sqlite"),
            SessionStoreCapabilities.Branching | SessionStoreCapabilities.Transactions,
            requiresDurableStore: true,
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
            o.Identity = identity;
            o.SecurityProfileKey = securityProfileKey;
            o.AgentDefinitionRevision = definitionRevision;
            o.ConfigurationVersion = configurationVersion;
            o.SessionProfile = sessionProfile;
            o.ModelSelectionPolicy = new ModelSelectionPolicy([alias]);
            o.ModelRequirements = ModelRequirements.None with { RequiresReasoning = true };
            o.RequestSettings = LlmRequestSettings.Default with { ReasoningEffort = configuration.ReasoningEffort };
            o.Instructions.Add(SystemMessage(agentId, workspaceRoot));
            o.MaxTurns = configuration.MaximumTurns;
            o.AttemptTimeout = TimeSpan.FromMinutes(3);
        });

        // Populates the model-facing tool list from whatever ITool instances the container resolves, without
        // an intermediate "probe" provider build: OptionsBuilder<T>.Configure<TDep> resolves TDep lazily, the
        // first time ConversationSessionOptions itself is materialized (when IConversationSession is resolved).
        _ = services.AddOptions<ConversationSessionOptions>()
            .Configure<IEnumerable<ITool>>(static (options, tools) =>
            {
                var descriptors = tools.Select(static tool => tool.Descriptor).ToImmutableArray();
                var definitions = descriptors.ToLlmToolDefinitions();
                for (var index = 0; index < descriptors.Length; index++)
                {
                    options.Tools.Add(definitions[index]);
                    options.ToolPresentationBindings.Add(new ConversationToolPresentationBinding(descriptors[index], definitions[index]));
                }
            });

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        return new OwnedConversationSession(provider.GetRequiredService<IConversationSession>(), provider);
    }

    /// <summary>Resolves the absolute application-owned session database path for one workspace.</summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <returns>The absolute configured override or deterministic workspace-keyed application-data path.</returns>
    internal static string SessionDatabasePath(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (!Path.IsPathFullyQualified(workspaceRoot))
        {
            throw new ArgumentException("The workspace root must be absolute.", nameof(workspaceRoot));
        }

        var configured = Environment.GetEnvironmentVariable("CODING_AGENT_SESSION_DB");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(LocalApplicationDataRoot(), "CodingAgent", "workspaces", WorkspaceKey(workspaceRoot), "sessions.db")
            : Path.IsPathFullyQualified(configured)
                ? Path.GetFullPath(configured)
                : throw new InvalidOperationException("CODING_AGENT_SESSION_DB must be an absolute path.");
    }

    /// <summary>Derives the stable agent identity that isolates one normalized workspace.</summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <returns>A deterministic non-default agent identity.</returns>
    internal static AgentId AgentIdForWorkspace(string workspaceRoot)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(NormalizedWorkspace(workspaceRoot)));
        return new AgentId(new Guid(bytes.AsSpan(0, 16)));
    }

    /// <summary>Hashes the normalized workspace into a content-free application-data directory name.</summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <returns>A stable lowercase SHA-256 value.</returns>
    private static string WorkspaceKey(string workspaceRoot) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizedWorkspace(workspaceRoot))));

    /// <summary>Canonicalizes an absolute workspace path for stable identity derivation.</summary>
    /// <param name="workspaceRoot">The absolute workspace root.</param>
    /// <returns>The full path without a trailing directory separator.</returns>
    private static string NormalizedWorkspace(string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        return Path.IsPathFullyQualified(workspaceRoot)
            ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot))
            : throw new ArgumentException("The workspace root must be absolute.", nameof(workspaceRoot));
    }

    /// <summary>Gets the platform-local application data authority selected by the host.</summary>
    /// <returns>An absolute application-data directory.</returns>
    private static string LocalApplicationDataRoot()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return !string.IsNullOrWhiteSpace(root) && Path.IsPathFullyQualified(root)
            ? root
            : throw new InvalidOperationException("A local application-data directory is required for durable sessions.");
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
        Use the read, write, edit, glob, search, command, todo, and question tools to inspect files,
        modify the workspace, track longer work, and ask one bounded question when a missing user choice blocks progress.
        Every tool path argument (including a command's working directory) must be relative to the
        workspace root, without a leading slash. Omit the working directory to use the workspace root itself.
        Prefer the smallest correct change. Explain what you did after acting.
        glob's "pattern" and search's "path_pattern" are path globs supporting only *, ?, and **. Neither tool
        consults .gitignore automatically. Use "exclude_patterns" for paths such as "**/bin/**", "**/obj/**",
        or other generated trees; an exclusion ending in /** prunes that matching directory before descent. search's
        own "pattern" argument is different: it is the text to find inside file contents (a literal substring,
        or a .NET regular expression when "regex" is true — the default), never a path glob; for example, to
        find "TODO" comments under src/AgentKit.Loop, call search with pattern "TODO", regex false, and
        base_path "src/AgentKit.Loop" — "pattern" here is always a plain string, never a number, and every
        other search argument (regex, case_sensitive, include_hidden, the maximum_* bounds) is optional with a
        sensible default, so omit any you don't need rather than guessing a value.
        A path-glob filters entries under "base_path"; it does not move the traversal root. Always set base_path
        to the narrowest directory that could contain what you're looking for, keep
        the path-glob relative to that base_path (for example base_path "src/AgentKit.Loop", pattern
        "**/*.cs"), and exclude irrelevant generated subtrees before a recursive search. If you don't yet know which
        directory to scope to, glob "*" with a shallow maximum_depth at the workspace root first to see the
        top-level layout, then narrow base_path from there before doing anything recursive. If a glob or
        search call fails because "base_path" does not exist, that is not the same as a match-free search —
        never report it as "no occurrences" or "nothing found"; instead glob the parent directory (or the
        workspace root, with a shallow maximum_depth) to see the real layout, then retry with a base_path
        you have actually confirmed exists.
        write requires an explicit "mode": use "create_only" when the target must be absent,
        "replace_existing" when it must already exist, "create_or_replace" when either target state is acceptable,
        or "append" when preserving existing content. Choose the narrowest disposition that matches the request.
        Trust only the tool result you actually receive, for every tool, not only write/edit/command: a
        failure (denied, timed out, base_path missing, limit exceeded, or any other error status) is never
        equivalent to an empty or negative result, and must never be reported to the user as one. Write, edit,
        and command calls specifically require the user's live approval and can be denied — if one reports
        denial or failure, tell the user plainly that the action did not happen, never claim to have made a
        change that was denied or failed. A denied action requires new user direction: stop attempting
        that action, explain what was blocked, and wait. Do not repeat the request or switch tools to
        perform the same denied change. A denial is not a transient execution failure.
        Reading tests, specifications, or source code is evidence about intended behavior, not verification that
        the behavior works. Only a successful actual test or command run verifies behavior. If requested verification
        is denied, expires, fails, or is cancelled, report it as unverified and do not call it confirmed or effectively
        confirmed through assertions.
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
