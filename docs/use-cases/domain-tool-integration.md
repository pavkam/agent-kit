# Order lookup with your own tool

An e-commerce company wants its assistant to answer "where is my order?" by
calling the order service the company already runs. Nothing in AgentKit knows
what an order is; the application does. The work is to wrap one application
service as a tool the model can request, describe its arguments precisely enough
that bad calls are rejected before they reach the service, and allow exactly
that tool and nothing else.

## What the agent needs

| Need                      | AgentKit part                                                                             |
| ------------------------- | ----------------------------------------------------------------------------------------- |
| A tool the model can call | Your class implementing `ITool` with a `ToolDescriptor` and JSON Schema for its arguments |
| Registration              | `builder.Services.AddTool<OrderLookupTool>()` or `AddSingleton<ITool>(instance)`          |
| Only this tool            | `AgentToolsOptions.AllowedToolIds` instead of `AllowAllRegisteredTools`                   |
| Customer scoping          | `ToolExecutionContext.Identity` inside the tool, never a customer id from the model       |

## Write the tool

A tool is a descriptor plus an `InvokeAsync`. The descriptor is what the model
sees and what the runtime validates arguments against; the invocation receives
already-validated JSON and the execution context:

```csharp
sealed class OrderLookupTool(IOrderService orders) : ITool
{
    public static readonly ToolId Id = new("lookup_order");

    static readonly JsonElement InputSchema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "order_number": { "type": "string", "pattern": "^[A-Z]{2}-[0-9]{6,10}$", "description": "The order number printed on the confirmation email." }
          },
          "required": ["order_number"],
          "additionalProperties": false
        }
        """).RootElement;

    public ToolDescriptor Descriptor { get; } = new(
        Id,
        new ToolVersion("1.0"),
        "lookup_order",
        "Looks up the status, items, and shipping progress of one of the customer's own orders.",
        new JsonSchema(new JsonSchemaDialectId("https://json-schema.org/draft/2020-12/schema"), InputSchema),
        outputSchema: null,
        new ToolEffects(ToolEffect.ReadOnly, IdempotencyClassification.ReadOnly, requiredResourceKinds: null),
        new ToolExecutionHints(ToolSchedulingMode.ParallelSafe, concurrencyKey: null, expectedDuration: TimeSpan.FromSeconds(1), approvalMayBeCached: null),
        new ToolSourceId("acme.orders"),
        ExtensionData.Empty);

    public async Task<ToolInvocationResult> InvokeAsync(ToolInvocationRequest request, CancellationToken cancellationToken = default)
    {
        var orderNumber = request.Arguments.GetProperty("order_number").GetString()!;
        var customer = request.Context.Identity.PrincipalId;   // who is asking, from the trusted ingress

        var order = await orders.FindAsync(customer, orderNumber, cancellationToken);
        if (order is null)
        {
            return Failed("No such order for this customer.", ToolTerminalStatus.InvalidArguments);
        }

        var text = $"Order {order.Number}: {order.Status}. Items: {string.Join(", ", order.Items)}. " +
                   $"Shipped {order.ShippedAt:d} via {order.Carrier}, tracking {order.Tracking}.";

        return new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.NotApplicable, retryable: false, failureReason: null, ExtensionData.Empty),
            [new TextPart(text, TextSemantics.Plain, ExtensionData.Empty)]);
    }

    // The outcome kind must agree with the terminal status: InvalidArguments is a rejection,
    // InvocationFailed is a failure. ToolTerminalStatus.ToOutcomeKind() encodes the mapping.
    static ToolInvocationResult Failed(string reason, ToolTerminalStatus status) => new(
        new ToolCallOutcome(status.ToOutcomeKind(), status, SideEffectCertainty.DefinitelyNotPerformed, retryable: false, reason, ExtensionData.Empty),
        [new TextPart(reason, TextSemantics.Plain, ExtensionData.Empty)]);
}
```

Three habits keep a tool honest:

- **Scope by identity, not by argument.** The customer is
  `request.Context.Identity`, carried from your authentication. The model can
  ask about any order number; the service only returns orders that belong to
  that principal.
- **Fail with a typed outcome, never an exception.** Unknown input returns a
  rejected or failed `ToolCallOutcome` with a safe reason the model can act on.
  Throwing is reserved for bugs.
- **Declare effects truthfully.** `ReadOnly` and `ParallelSafe` let the
  scheduler run several lookups concurrently. A tool that changes state declares
  `ToolEffect.Mutating` and, if it touches a protected boundary, asks the
  `ISecurityAuthority` for a grant before acting, exactly as the first-party
  tools do.

## Compose the engine

```csharp
static AgentEngine CreateOrderAssistant(ExecutionIdentity customer, IOrderService orders, string apiKey)
{
    var builder = AgentEngine.CreateBuilder()
        .UseLocalDevelopmentDefaults()
        .UseOpenAI(apiKey, "gpt-4o-mini")
        .WithIdentity(customer)
        .WithInstructions(
            "You help customers with their orders. Use lookup_order when asked about an " +
            "order; ask for the order number if it is missing. Do not guess order contents.")
        .WithMaxTurns(6);

    builder.Services.AddSingleton(orders);
    builder.Services.AddTool<OrderLookupTool>();

    // Name the tools rather than inheriting the local allow-all.
    builder.Services.Configure<AgentToolsOptions>(o =>
    {
        o.AllowAllRegisteredTools = false;
        o.AllowedToolIds.Add(OrderLookupTool.Id);
    });

    return builder.Build();
}
```

Every `ITool` registered on `builder.Services` enters the tool catalog, but only
the allowed ones are added to the published agent definition and presented to
the model, so the model never spends a turn on a tool whose call would be
rejected. The allow-list is also enforced at call time: a call to an unlisted
tool, however it got into the request, is rejected with a typed denied result
before `InvokeAsync` runs.

## Use it

```csharp
await using var engine = CreateOrderAssistant(customer, orders, apiKey);

var result = await engine.SendAsync("Where is my order UK-00123456?", cancellationToken);

foreach (var call in result.Events.OfType<ConversationToolCallEvent>())
{
    Console.WriteLine($"{call.ToolName} {call.ArgumentsJson}");
}

Console.WriteLine(string.Concat(result.Events.OfType<ConversationAssistantTextEvent>().Select(e => e.Text)));
```

## What the framework guarantees

- **Arguments are validated before your code runs.** The runtime compiles the
  descriptor's schema under bounded limits and rejects a call whose arguments do
  not conform (`additionalProperties`, the pattern, the required field) with
  `ToolTerminalStatus.InvalidArguments`; `InvokeAsync` sees only valid JSON.
- **The model requests; it never executes.** Every call passes catalog
  resolution, schema validation, and the tool authorizer before invocation.
  Unknown tools fail closed.
- **Correlation survives.** The `ToolCallId` the model chose is preserved
  through authorization, execution, the `ToolCallResult` record, and the message
  the model sees next; the two events above carry the same `CallId`.
- **Descriptions grant nothing.** Tool metadata is untrusted input to the model.
  Authority comes only from the allow-list and security policies.

## What lives where

| Concern                                | Package                                                                                                            |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `ITool`, `ToolDescriptor`, outcomes    | [AgentKit.Abstractions](../../src/AgentKit.Abstractions/README.md)                                                 |
| Catalog, schema validation, authorizer | [AgentKit.Tools](../../src/AgentKit.Tools/README.md)                                                               |
| A first-party tool to copy from        | [AgentKit.Tools.Read](../../src/AgentKit.Tools.Read/README.md)                                                     |
| Normative tool rules                   | [Tools and toolsets](../concepts/tools-and-toolsets.md), [Tool call lifecycle](../concepts/tool-call-lifecycle.md) |

Next: [Delegating to specialist agents](delegating-to-specialists.md) ·
[Background ticket-triage worker](ticket-triage-worker.md)
