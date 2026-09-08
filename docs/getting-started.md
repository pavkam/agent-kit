# Getting started

This walkthrough runs AgentKit's model catalog through its public DI surface. It
needs no credentials and sends no requests to a model provider. You will
register a model description, inspect the catalog, and see where model execution
fits in a larger application.

## Current status

AgentKit targets .NET 10 and is currently versioned `0.1.0-alpha.1` in
[Directory.Build.props](../Directory.Build.props). Work from a source checkout
when evaluating these instructions. The repository's version number does not
establish availability on a public package feed.

Source implementations exist for the facade, loops, context, output, budgets,
identity, permissions, sessions, artifacts, providers, MCP, and tools. Complete
end-to-end composition and architectural conformance remain under development.
There is no complete first-agent application in this checkout yet.

Durable execution, cross-session memory/retrieval, evaluation, and some storage
backends remain architectural requirements without their full runtime packages.
See the [implementation ledger](implementation-progress.md#component-coverage)
for open work. A project or passing test suite establishes only the behavior it
actually implements and exercises.

## Set up the repository

Install the SDK selected by [global.json](../global.json), currently .NET
10.0.203 with compatible patch roll-forward. Node.js 22 or later and GNU Make
are needed for repository tooling.

```sh
git clone https://github.com/pavkam/agent-kit.git
cd agent-kit
make restore
make build
```

## Create a small application

From the repository root, create a console application alongside your checkout
and reference the provider catalog project. Keeping it outside the checkout lets
the application use its own project defaults instead of the framework
repository's contributor analyzers:

```sh
dotnet new console --framework net10.0 --output ../AgentKitCatalogDemo
dotnet add ../AgentKitCatalogDemo/AgentKitCatalogDemo.csproj reference src/AgentKit.Providers/AgentKit.Providers.csproj
```

Replace `../AgentKitCatalogDemo/Program.cs` with:

```csharp
using AgentKit;
using AgentKit.Providers;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAgentProviders();

await using var provider = services.BuildServiceProvider();
var catalog = provider.GetRequiredService<IModelCatalog>();
var snapshot = await catalog.GetSnapshotAsync(CancellationToken.None);

Console.WriteLine($"Configured models: {snapshot.ConversationModels.Length}");
```

Run it:

```sh
dotnet run --project ../AgentKitCatalogDemo/AgentKitCatalogDemo.csproj
```

The application prints `Configured models: 0`. `AddAgentProviders` registers the
catalog and selection services; your application supplies model descriptors and
concrete implementations separately.

## Register a model descriptor

Replace the program with this complete example:

```csharp
using AgentKit;
using AgentKit.Providers;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAgentProviders();
services.AddModelDescriptors(
    new ModelDescriptorSourceId("demo"),
    [
        new ModelDescriptor(
            new ModelAlias("assistant"),
            new ProviderId("demo-provider"),
            new ApiFamilyId("demo-api"),
            new ModelId("demo-model"),
            null,
            new ModelCapabilities(
                supportsSystemInstructions: true,
                supportsStreaming: false,
                supportsToolCalls: false,
                supportsParallelToolCalls: false,
                supportsStructuredOutput: false,
                supportsReasoning: false,
                supportsVisionInput: false,
                extensions: ExtensionData.Empty),
            new ModelLimits(null, null),
            null,
            ExtensionData.Empty)
    ]);

await using var provider = services.BuildServiceProvider();
var catalog = provider.GetRequiredService<IModelCatalog>();
var snapshot = await catalog.GetSnapshotAsync(CancellationToken.None);

foreach (var model in snapshot.ConversationModels)
{
    Console.WriteLine($"{model.Alias.Value}: {model.ModelId.Value}");
}
```

Run the same command again. It prints `assistant: demo-model`.

The identifiers above describe a fictional model for this local example. No
model implementation or credential has been registered, so this descriptor
cannot generate a response. Its unknown token limits stay `null`; they do not
mean unlimited capacity.

The application uses the alias `assistant` independently of a provider's model
ID. In a real integration, a
[provider package](packages/index.md#model-providers) supplies the model
implementation, and the host configures supported capabilities,
endpoint/service-surface profiles, credentials, and permission policy. Duplicate
descriptor aliases are rejected when the catalog is read.

## Move toward a complete agent

Read [Composing an application](guides/composition.md) for the engine's required
collaborators and lifecycle. Then choose a provider and tools from the
[project catalog](packages/index.md). Each project README links its registration
API, tests, and relevant specification.

For a targeted view of the catalog's behavior, run:

```sh
dotnet test --project tests/AgentKit.Providers.Tests/AgentKit.Providers.Tests.csproj --configuration Release --timeout 300s
```
