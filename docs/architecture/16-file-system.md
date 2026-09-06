# File system

**Role:** Make file and directory access replaceable, policy-aware, and
deterministic in tests.

Framework and tool code never calls operating-system file APIs directly. It
depends on provider-neutral file-system contracts from AgentKit.Abstractions.
Applications may replace the implementation globally or within an explicitly
scoped engine composition.

## Contract boundaries

The abstraction covers the operations the framework actually needs, including
file reads and writes, directory enumeration, metadata, path normalization,
temporary storage, and optional change observation. Consumers depend on narrow
capability contracts rather than receiving authority to every filesystem
operation through one oversized service.

Paths are logical values with an explicit root and comparison policy. The
abstraction defines normalization, symbolic-link behavior, case sensitivity,
atomic replacement, stream ownership, cancellation, and failure categories. Raw
host paths do not become portable identifiers by accident.

## Implementations

AgentKit.FileSystem is the operating-system implementation. It translates
neutral operations to System.IO while enforcing configured roots, bounds, and
ownership. AgentKit.FileSystem.InMemory is a deterministic implementation for
tests, examples, and applications that need an isolated virtual filesystem.

Applications can provide other implementations, such as a workspace sandbox,
remote object store, browser-backed filesystem, or read-only projection, without
changing tools or the loop.

## Tools and permissions

AgentKit.Tools.Read, AgentKit.Tools.Write, AgentKit.Tools.Skill, and any other
file-aware package depend only on the file-system abstractions. They do not
reference AgentKit.FileSystem.

The file system does not grant tool authority. A tool call first passes through
the permission policy, which determines approved roots, resources, and effects.
The implementation enforces the resulting scope and fails closed when it cannot
represent it. A sandbox may further restrict the operation but never replaces
authorization.

## Time and testing

The in-memory implementation uses the injected TimeProvider for created,
modified, expiry, and watcher timestamps. The operating-system implementation
reports filesystem metadata as external truth while using TimeProvider for
framework-owned deadlines, polling, retries, and event times.

Shared conformance suites run against both implementations. They cover path
normalization, read/write behavior, atomic replacement, enumeration order,
stream disposal, cancellation, metadata, root escape attempts, symbolic links,
and concurrent access. Tests do not touch the developer's real workspace unless
they are explicitly marked integration tests and use an isolated temporary root.

## Related architecture

- [Project structure](project-structure.md)
- [Tools](07-tools.md)
- [Permissions and human control](08-permissions-and-human-control.md)
