// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using CodingAgent;

DotEnvLoader.LoadIfPresent();

if (args is ["--smoke-test", .. var rest])
{
    return await CodingAgentSmokeTest.RunAsync(rest);
}

var workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var themeSlug = CodingAgentTheme.ResolveSlugFromEnvironment();
var status = await ConsoleApplication.RunAsync(
    new ChatScreen(workspaceRoot, themeSlug),
    builder => builder.UseTheme(CodingAgentTheme.Load(themeSlug)));
return status == ConsoleRunStatus.Failed ? 1 : 0;
