// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Collections.Immutable;
global using System.Diagnostics;

global using AgentKit;
global using AgentKit.Context;
global using AgentKit.Conversations;
global using AgentKit.FileSystem;
global using AgentKit.Loop;
global using AgentKit.Output;
global using AgentKit.Permissions;
global using AgentKit.Permissions.InMemory;
global using AgentKit.Providers;
global using AgentKit.Providers.Anthropic;
global using AgentKit.Providers.AzureOpenAI;
global using AgentKit.Providers.Ollama;
global using AgentKit.Providers.OpenAI;
global using AgentKit.Providers.OpenRouter;
global using AgentKit.Session;
global using AgentKit.Session.InMemory;
global using AgentKit.Session.Sqlite;
global using AgentKit.Tools;
global using AgentKit.Tools.Edit;
global using AgentKit.Tools.Glob;
global using AgentKit.Tools.List;
global using AgentKit.Tools.Read;
global using AgentKit.Tools.Search;
global using AgentKit.Tools.Write;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Options;
