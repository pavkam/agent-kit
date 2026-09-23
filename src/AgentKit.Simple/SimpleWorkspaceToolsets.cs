// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Simple;

using AgentKit.Tools;
using AgentKit.Tools.Edit;
using AgentKit.Tools.Glob;
using AgentKit.Tools.List;
using AgentKit.Tools.Patch;
using AgentKit.Tools.Read;
using AgentKit.Tools.Search;
using AgentKit.Tools.Write;

/// <summary>Publishes the workspace file-tool surface Simple agents compose through <see cref="AgentEngineBuilderExtensions.UseWorkspace"/>.</summary>
public static class SimpleWorkspaceToolsets
{
    /// <summary>Gets the toolset key for the bundled workspace file tools.</summary>
    public static ToolsetKey Key { get; } = new("agentkit.simple.workspace");

    /// <summary>Gets the publication selecting every workspace file tool from the application tool source.</summary>
    public static ToolsetPublication Publication { get; } = new(
        Key,
        new ToolsetVersion(1),
        new ToolExecutionPolicyReference(SimpleToolRuntime.StandardExecutionPolicyKey, new ToolExecutionPolicyVersion(1)),
        [new ToolsetSourceSelection(ApplicationToolSources.Default)],
        [
            new ToolAliasAssignment(new ToolAlias("read_file"), new ToolIdentity(ReadFileTool.Id, ReadFileTool.Descriptor.Version)),
            new ToolAliasAssignment(new ToolAlias("write_file"), new ToolIdentity(WriteFileTool.Id, WriteFileTool.Descriptor.Version)),
            new ToolAliasAssignment(new ToolAlias("list_directory"), new ToolIdentity(ListDirectoryTool.Id, ListDirectoryTool.Descriptor.Version)),
            new ToolAliasAssignment(new ToolAlias("glob"), new ToolIdentity(GlobTool.Id, GlobTool.Descriptor.Version)),
            new ToolAliasAssignment(new ToolAlias("search"), new ToolIdentity(SearchTool.Id, SearchTool.Descriptor.Version)),
            new ToolAliasAssignment(new ToolAlias("edit"), new ToolIdentity(EditTool.Id, EditTool.Descriptor.Version)),
            new ToolAliasAssignment(new ToolAlias("patch"), new ToolIdentity(PatchTool.Id, PatchTool.Descriptor.Version)),
        ]);
}
