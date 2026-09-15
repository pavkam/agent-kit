# Working with files

Most useful agents need to look at files, and many need to change them. AgentKit
gives an agent a **workspace**: one directory it can see, behind a sandbox that
enforces the boundary on every call, with tools the model can use to read,
search, and edit inside it.

## Give the agent a directory

```csharp
await using var engine = AgentEngine.CreateBuilder()
    .UseLocalDevelopmentDefaults()
    .UseOpenAI(apiKey, "gpt-4o-mini")
    .UseWorkspace("/home/me/projects/website")
    .Build();

Console.WriteLine(await engine.AskAsync("Which pages link to the pricing page?"));
```

`UseWorkspace` does two things. It registers a sandboxed file system rooted at
that directory, and it registers six tools over it that the model sees on its
next turn:

| Tool             | What the model can do with it                                         |
| ---------------- | --------------------------------------------------------------------- |
| `read_file`      | Read a text file, optionally a line range                             |
| `list_directory` | List a directory, paged                                               |
| `glob`           | Find files by pattern, such as `src/**/*.cs`, with exclusions         |
| `search`         | Find text or a regular expression inside files, with bounded results  |
| `write_file`     | Create or replace a file with an explicit disposition (see below)     |
| `edit`           | Replace exact text in a file, once or everywhere, with a change count |

Every path the model supplies is relative to the workspace root. The root must
be absolute.

## What the sandbox guarantees

The sandbox is the thing that makes "give the agent a directory" safe to say. On
every call it:

- **Resolves paths inside the root only.** `..` segments are rejected before
  anything is opened, and symbolic links are never followed across the boundary,
  so a link inside the workspace that points outside it is refused rather than
  read.
- **Bounds every operation.** Reads, writes, searches, and directory listings
  have size, count, depth, and time limits with sensible defaults. A file larger
  than the read limit is reported as a limit, not silently truncated.
- **Writes with a stated intent.** `write_file` requires a disposition:
  `create_only` (fail if the file exists), `replace_existing` (fail if it does
  not), `create_or_replace`, or `append` (fail if it does not exist). The model
  must say which outcome it wants, and the file system holds it to that.
- **Enforces the grant again.** The security decision that allowed a call (next
  section) is checked once more at the file system, bound to the exact path and
  effect, so a higher-level "allow" can never authorize a different concrete
  file.

Adjust the limits when the defaults do not fit:

```csharp
.UseWorkspace("/data/corpus", options =>
{
    options.MaximumReadBytes = 50 * 1024 * 1024;
    options.MaximumSearchFiles = 100_000;
})
```

## Who decides whether a write happens

Registering a tool does not grant it anything. Before any file tool runs, the
runtime asks the security authority whether this exact operation, on this exact
path, with this exact effect, is allowed. With `UseLocalDevelopmentDefaults()`
the answer is always yes, which is what you want for a personal tool on your own
machine and never what you want anywhere else.

Two common steps up, both covered in
[Permissions and approvals](permissions.md):

- **Read-only agent.** Add a policy that denies `FileWrite` and
  `DirectoryCreate`; deny wins over the default allow, so `write_file` and
  `edit` are refused before they touch anything, and the model is told so.
- **Ask before writing.** Add a policy that returns `RequireApproval` for writes
  and an approval handler that asks you. The
  [CodingAgent](../../examples/CodingAgent/README.md) example does exactly this
  with a terminal prompt.

## Test without a disk

`AgentKit.FileSystem.InMemory` implements the same seven file-system contracts
over an in-memory tree, with the same status codes and precondition ordering.
Register it in place of the sandbox in tests, seed files, and drive the tools
without touching the real file system:

```csharp
builder.Services.AddInMemoryFileSystem();
builder.Services.AddReadTool().AddWriteTool();   // any subset of the tools
```

## Under the hood

`UseWorkspace(root)` is this, which a host that owns its own
`IServiceCollection` writes directly:

```csharp
services.AddSandboxedFileSystem(root);
services.AddReadTool();
services.AddWriteTool();
services.AddEditTool();
services.AddGlobTool();
services.AddSearchTool();
services.AddListTool();
```

Register only the tools you want the model to have. Processes (running a build
or a test suite) are a separate boundary with its own sandbox and its own
permission; see [Permissions and approvals](permissions.md) and the
[process execution architecture](../architecture/process-execution.md).

Next: [Permissions and approvals](permissions.md) ·
[Storing conversations](storage.md)
