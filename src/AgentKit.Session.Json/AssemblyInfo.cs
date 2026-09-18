// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AgentKit.Session.Json.Tests")]
[assembly: SuppressMessage(
    "Style",
    "IDE0051:Remove unused private members",
    Scope = "member",
    Target = "~M:AgentKit.Session.Json.ServiceExtensions.AddSessionEntryCodecs(Microsoft.Extensions.DependencyInjection.IServiceCollection)",
    Justification = "The shared codec registration is called only from C# 14 extension-block members, which IDE0051 " +
        "does not currently recognize as call sites; removing the member breaks the session-store registration.")]
[assembly: SuppressMessage(
    "Style",
    "IDE0051:Remove unused private members",
    Scope = "member",
    Target = "~M:AgentKit.Session.Json.ServiceExtensions.Capture``2(Microsoft.Extensions.DependencyInjection.IServiceCollection,``0,``1,System.String)",
    Justification = "The shared registration capture is called only from C# 14 extension-block members, which IDE0051 " +
        "does not currently recognize as call sites; removing the member breaks both leaf registrations.")]
