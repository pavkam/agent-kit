// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Collections.Immutable;
global using System.Diagnostics;
global using System.Diagnostics.Metrics;
global using System.Text;
global using System.Text.Json;
global using System.Text.Json.Serialization;

global using AgentKit;
global using AgentKit.Observability;
global using AgentKit.Permissions.Json;
global using AgentKit.Storage.Json;
global using AgentKit.TestSupport;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Time.Testing;

global using Shouldly;

global using Xunit;
