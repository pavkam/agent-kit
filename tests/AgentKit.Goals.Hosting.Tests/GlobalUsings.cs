// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Collections.Concurrent;
global using System.Collections.Immutable;
global using System.Diagnostics;

global using AgentKit;
global using AgentKit.Goals;
global using AgentKit.Goals.Hosting;
global using AgentKit.Observability;
global using AgentKit.TestSupport;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.Time.Testing;

global using Shouldly;

global using Xunit;
