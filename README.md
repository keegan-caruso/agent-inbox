# agent-inbox

A CLI tool for inter-agent communication on a single machine, backed by a local SQLite database.

## Overview

`agent-inbox` allows AI agents (or any processes) to register themselves, send messages to each other, and read their inboxes — all via a simple command-line interface. Data is stored in a local SQLite database.

The system is intended for mutually trusted local processes. Capability tokens authorize message actions; agent IDs are addresses, not proof of authority. Agent discovery through `agent-inbox agents` is intentionally public within that trust model.

Groups are named recipient sets with persistent membership. Groups are reusable send targets and are expanded to active member agents at send time. They are not shared inboxes or special conversation objects.

## Known Limitations

- Migration for older or unversioned databases is not yet implemented; only fresh databases and current-version databases are supported.

## Requirements

- .NET 10 SDK (for building from source)
- .NET 10 Runtime (for running as a .NET tool)

> `agent-inbox` must remain publishable and functional under Native AOT-compatible coding patterns; tests must avoid introducing non-AOT-safe implementation dependencies.

## Installation

### Install as a .NET Tool

The easiest way to install `agent-inbox` is as a .NET global tool:

```bash
dotnet tool install --global AgentInbox
```

Once installed, the `agent-inbox` command will be available globally.

To update to the latest version:

```bash
dotnet tool update --global AgentInbox
```

To uninstall:

```bash
dotnet tool uninstall --global AgentInbox
```

### Build from Source

```bash
dotnet build src/AgentInbox/AgentInbox.csproj -c Release
```

## Versioning

This project follows [Semantic Versioning](https://semver.org/). Release notes are tracked in [CHANGELOG.md](CHANGELOG.md).

## Run Tests

```bash
dotnet publish tests/AgentInbox.Tests/AgentInbox.Tests.csproj -c Release -r linux-x64
./tests/AgentInbox.Tests/bin/Release/net10.0/linux-x64/publish/AgentInbox.Tests
```

## Usage

See [Command Reference](docs/commands.md) for the full CLI usage,
global options, capability token handling, and every command.

## Output Formats

- **plain**: Human-readable tabular output (default)
- **json**: JSON array or object
- **ndjson**: Newline-delimited JSON (one object per line); useful for streaming/piping

## Database

The SQLite database is created automatically on first use. Schema versioning uses SQLite `PRAGMA user_version`.

- **Fresh database**: initialized to the latest schema and assigned `user_version = 1`.
- **Current-version database** (`user_version = 1`): opened normally.
- **Unversioned legacy database** (tables present, `user_version = 0`): rejected; migration is not implemented yet.
- **Newer database** (`user_version > 1`): rejected; created by a future binary this version does not support.

Future schema changes will increment `user_version` and be handled version-to-version. Backward compatibility for older databases is not implemented yet.

Schema tables:

- **agents**: Registered agents with optional display names, capability token hashes, token creation timestamps, and soft-delete support
- **messages**: Messages with sender, subject, body, and optional reply threading
- **message_recipients**: Many-to-many join table tracking delivery and read status
- **groups**: Named recipient sets (soft-delete supported)
- **group_members**: Group membership edges between groups and agents
- **messages_fts**: FTS5 virtual table for full-text search (always present; mirrors `messages`)
- **message_embeddings**: sqlite-vec `vec0` virtual table for semantic vector search (present when the sqlite-vec extension loads successfully; 384-dimensional float vectors)

## Security and Trust Model

- `agent-inbox` is designed for mutually trusted local processes on the same machine.
- Capability tokens authorize message actions like sending, replying, reading, and viewing inbox contents.
- Agent IDs identify message addresses and are not treated as proof of authority for message actions by themselves.
- Some non-message operations, such as `deregister <agent-id>`, are intentionally left unauthenticated in this local, mutually trusted setting.
- `agents` discovery is intentionally allowed so local processes can find active recipients.

## Breaking Changes

- `send` and `reply` no longer accept `--from`.
- `read` no longer accepts `--as`.
- `inbox` no longer accepts an agent ID positional argument.
- Use `--token` or `AGENT_INBOX_CAPABILITY_TOKEN` instead.

## Example Workflow

```bash
# Register agents
agent-inbox register alice --display-name "Alice"
# Agent 'alice' registered successfully.
# Agent ID: alice
# Capability Token: 0195f0f9-4e18-7a4e-a0fa-d0d76c8dc2f3

agent-inbox register bob --display-name "Bob"
# Agent 'bob' registered successfully.
# Agent ID: bob
# Capability Token: 0195f0f9-5734-7b4f-8d55-61e4b4cb6ec6

# Export tokens to avoid repeated --token values in shell history
export ALICE_TOKEN=0195f0f9-4e18-7a4e-a0fa-d0d76c8dc2f3
export BOB_TOKEN=0195f0f9-5734-7b4f-8d55-61e4b4cb6ec6
export AGENT_INBOX_CAPABILITY_TOKEN="$BOB_TOKEN"

# List agents
agent-inbox agents

# Send a message
agent-inbox send --token "$ALICE_TOKEN" --to bob --subject "Hello" --body "Hi Bob, how are you?"

# Check inbox
agent-inbox inbox

# Read a message (marks as read)
agent-inbox read 1

# Reply
agent-inbox reply --to-message 1 --body "Hi Alice! I'm doing great."

# Check Alice's inbox for the reply
AGENT_INBOX_CAPABILITY_TOKEN="$ALICE_TOKEN" agent-inbox inbox
```
