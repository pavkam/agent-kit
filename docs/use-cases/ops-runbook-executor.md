# Operations runbook executor

An on-call engineer has a folder of runbooks: Markdown files that say "check the
queue depth, then if it is above N restart the consumer". They want an agent
that reads the runbook, runs the diagnostic commands, asks the engineer which
remediation to take when the runbook offers a choice, and never runs a command
the engineer did not approve. The shell it gets is confined to a working
directory, a short list of executables, and no network.

## What the agent needs

| Need                         | AgentKit part                                                                                |
| ---------------------------- | -------------------------------------------------------------------------------------------- |
| Read the runbooks            | `AddSandboxedFileSystem(root)` with `AddReadTool`, `AddGlobTool`, `AddListTool`              |
| Run shell commands, confined | `AddOperatingSystemProcesses(root, …)` with an executable allow-list and `AddCommandTool`    |
| No network from commands     | `PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile`                                   |
| Approve each command         | A policy returning `RequireApproval` for `Process`/`Execute` plus the approval collaborators |
| Ask the engineer to choose   | `AddQuestionTool`, `AddHumanQuestionBroker`, and your `IHumanQuestionChannel`                |

## Compose the engine

```csharp
static AgentEngine CreateRunbookAgent(string opsRoot, ExecutionIdentity engineer, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .WithIdentity(engineer)
        .WithInstructions(
            "You execute operational runbooks found under runbooks/. Run one " +
            "diagnostic at a time and report its output. When a runbook offers a " +
            "choice, ask the engineer with the question tool before acting.")
        .WithMaxTurns(40)
        .WithAttemptTimeout(TimeSpan.FromMinutes(5));

    // Read-only view of the runbooks and any scripts they reference.
    builder.Services.AddSandboxedFileSystem(opsRoot);
    builder.Services.AddReadTool();
    builder.Services.AddGlobTool();
    builder.Services.AddListTool();

    // A shell with a fixed executable list, an explicit PATH, and no network.
    builder.Services.AddOperatingSystemProcesses(opsRoot, o =>
    {
        o.AllowedExecutablePaths.Add("/bin/sh");
        o.AllowedEnvironmentVariableNames.Add("PATH");
        o.ReadOnlyToolchainRoots["cli"] = "/opt/acme/bin";
        o.MaximumTimeout = TimeSpan.FromMinutes(2);
        o.MaximumConcurrentProcesses = 1;
    });
    builder.Services.AddCommandTool(o =>
    {
        o.SandboxProfile = PlatformProcessSandboxProvider.WorkspaceNoNetworkProfile;
        o.EnvironmentVariables["PATH"] = "/opt/acme/bin:/usr/bin:/bin";
        o.DefaultTimeout = TimeSpan.FromSeconds(30);
    });

    // Every command execution is approved by the engineer.
    builder.Services.AddSingleton<ISecurityPolicy, ApproveCommandsPolicy>();
    builder.Services.AddInMemoryApprovalStore();
    builder.Services.RemoveAll<IApprovalHandler>();
    builder.Services.AddSingleton<IApprovalHandler>(new ConsoleApprovalHandler(engineer, TimeProvider.System));
    builder.Services.RemoveAll<IApprovalResponderAuthorizer>();
    builder.Services.AddSingleton<IApprovalResponderAuthorizer>(new SameUserMayApprove(engineer));

    // The model can put a bounded multiple-choice question to the engineer.
    builder.Services.AddSingleton<IHumanQuestionChannel>(new ConsoleQuestionChannel(engineer, TimeProvider.System));
    builder.Services.AddHumanQuestionBroker();
    builder.Services.AddQuestionTool(o => o.DefaultTimeout = TimeSpan.FromMinutes(2));

    return builder.Build();
}
```

```csharp
sealed class ApproveCommandsPolicy : ISecurityPolicy
{
    public ValueTask<SecurityPolicyResult> EvaluateAsync(SecurityRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(request.Kind == SecurityOperationKind.Process && request.Effect == SecurityEffect.Execute
            ? new SecurityPolicyResult(SecurityPolicyResultKind.RequireApproval, "ops.approve-command", "Each command needs the on-call engineer's approval.")
            : new SecurityPolicyResult(SecurityPolicyResultKind.Abstain, null, null));
}
```

The question channel renders the prompt and returns a typed result. The broker
has already checked that the question belongs to this identity and session; the
channel only has to answer or say it cannot:

```csharp
sealed class ConsoleQuestionChannel(ExecutionIdentity engineer, TimeProvider clock) : IHumanQuestionChannel
{
    public ValueTask<HumanQuestionResult> AskAsync(HumanQuestionPrompt prompt, CancellationToken cancellationToken = default)
    {
        Console.WriteLine(prompt.Prompt);
        for (var i = 0; i < prompt.Options.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {prompt.Options[i].Label} - {prompt.Options[i].Description}");
        }

        Console.Write("> ");
        var line = Console.ReadLine();
        if (clock.GetUtcNow() >= prompt.Deadline || !int.TryParse(line, out var choice) || choice < 1 || choice > prompt.Options.Length)
        {
            return ValueTask.FromResult<HumanQuestionResult>(new HumanQuestionTimedOut(prompt.Id));
        }

        var answer = new HumanQuestionAnswer(prompt.Options[choice - 1].Id, freeText: null, engineer, clock.GetUtcNow());
        return ValueTask.FromResult<HumanQuestionResult>(new HumanQuestionAnswered(prompt.Id, answer));
    }
}
```

## Use it

```csharp
await using var engine = CreateRunbookAgent("/srv/ops", engineer, apiKey);

var result = await engine.SendAsync(
    "Alert: orders-consumer lag is high. Follow runbooks/orders-consumer-lag.md.",
    new ConsoleProgress(),
    cancellationToken);
```

A typical turn prints the runbook step the model chose, an approval prompt
naming the exact command, the bounded output the command produced, a question
when the runbook branches, and the model's summary. `ConsoleProgress` is the
observer from
[Documentation maintainer with approval](docs-maintainer-with-approval.md),
watching `command` and `question` instead of the write tools.

## What the framework guarantees

- **The executable list is closed.** `AllowedExecutablePaths` is resolved and
  fingerprinted before authorization; a command that names another binary, or a
  script outside the workspace and toolchain roots, is rejected by the resolver
  before any policy sees it.
- **The sandbox limits consequences; it grants nothing.** The no-network profile
  is applied through the platform sandbox (`sandbox-exec` on macOS, `bwrap` on
  Linux). Where the profile is unavailable the command tool fails closed rather
  than running unsandboxed.
- **Approval names the effect.** The prompt the engineer sees is built from the
  resolved intent: the executable, working directory, and arguments the runner
  will actually use, not the string the model typed.
- **Output is bounded and the process is reaped.** `MaximumOutputBytes`, the
  per-command timeout, and `ForcedTerminationWait` are enforced by the runner; a
  timed-out command is terminated and reported as such.
- **A question is bound to the run.** The broker rejects a question for another
  identity or session; a timed-out or unavailable answer is a typed result the
  model sees, never a fabricated choice.

## What lives where

| Concern                             | Package                                                                             |
| ----------------------------------- | ----------------------------------------------------------------------------------- |
| Process resolution, sandbox, runner | [AgentKit.Processes](../../src/AgentKit.Processes/README.md)                        |
| `command` tool                      | [AgentKit.Tools.Command](../../src/AgentKit.Tools.Command/README.md)                |
| `question` tool                     | [AgentKit.Tools.Question](../../src/AgentKit.Tools.Question/README.md)              |
| Human-question broker               | [AgentKit.IO](../../src/AgentKit.IO/README.md)                                      |
| Approval flow                       | [AgentKit.Permissions](../../src/AgentKit.Permissions/README.md)                    |
| Normative process rules             | [Process execution and sandboxing](../concepts/process-execution-and-sandboxing.md) |

Next: [Local private assistant on Ollama](local-private-assistant.md) ·
[Example: CodingAgent](coding-agent-example.md)
