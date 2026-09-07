// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

global using System.Collections.Immutable;
global using System.Diagnostics;
global using System.Diagnostics.Metrics;

global using AgentKit;
global using AgentKit.Identity;
global using AgentKit.Observability;

global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using Microsoft.Extensions.Options;
global using Microsoft.Extensions.Time.Testing;

global using Shouldly;

global using Xunit;

global using static AgentKit.Identity.Tests.IdentityTestData;
