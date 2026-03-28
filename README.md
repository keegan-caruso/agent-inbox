# agent-inbox

A CLI tool for inter-agent communication on a single machine, backed by a local SQLite database.

## Overview

`agent-inbox` allows AI agents (or any processes) to register themselves, send messages to each other, and read their inboxes — all via a simple command-line interface. Data is stored in a local SQLite database (`~/.agent-inbox/inbox.db` by default).

The system is intended for mutually trusted local processes. Capability tokens authorize message actions; agent IDs are addresses, not proof of authority. Agent discovery through `agent-inbox agents` is intentionally allowed.

## Known Limitations

- Capability-token schema changes currently assume a fresh database.
- Database migration and backward compatibility for older databases are not handled yet.

## Requirements

- .NET 10 SDK

> `agent-inbox` must remain publishable and functional under Native AOT-compatible coding patterns; tests must avoid introducing non-AOT-safe implementation dependencies.

## Build

```bash
dotnet build src/AgentInbox/AgentInbox.csproj -c Release
```

## Run Tests

```bash
dotnet publish tests/AgentInbox.Tests/AgentInbox.Tests.csproj -c Release -r linux-x64
./tests/AgentInbox.Tests/bin/Release/net10.0/linux-x64/publish/AgentInbox.Tests
```

## Usage

```bash
agent-inbox [--db-path <path>] [--format plain|json|ndjson] <command> [options]
```

### Global Options

| Option | Default | Description |
|--------|---------|-------------|
| `--db-path` | `~/.agent-inbox/inbox.db` | Path to the SQLite database file |
| `--format`, `-f` | `plain` | Output format: `plain`, `json`, or `ndjson` |

### Capability Tokens

- `register` returns a capability token for the agent.
- `send`, `reply`, `read`, and `inbox` require a capability token.
- Provide the token with `--token` or `AGENT_INBOX_CAPABILITY_TOKEN`.
- `--token` takes precedence over `AGENT_INBOX_CAPABILITY_TOKEN`.
- Prefer the environment variable when possible to reduce shell-history exposure.

### Commands

#### `register <agent-id> [--display-name <name>]`

Register a new agent. If the agent was previously deregistered, it will be reactivated.

```bash
agent-inbox register my-agent --display-name "My Agent"
# Agent 'my-agent' registered successfully.
# Agent ID: my-agent
# Capability Token: 0195f0f9-4e18-7a4e-a0fa-d0d76c8dc2f3
```

#### `deregister <agent-id>`

Soft-delete an agent (marks it as deregistered without removing data).

```bash
agent-inbox deregister my-agent
```

#### `agents`

List all currently active (non-deregistered) agents.

```bash
agent-inbox agents
agent-inbox agents --format json
```

Discovery is intentionally public; this command does not require a capability token.

#### `send --token <capability-token> --to <agent-id[,agent-id,...]> --body <text> [--subject <text>]`

Send a message from the agent authorized by the capability token to one or more recipients.

```bash
agent-inbox send --token "$ALICE_TOKEN" --to bob --subject "Hello" --body "Hi Bob!"
agent-inbox send --token "$ALICE_TOKEN" --to bob,carol --body "Group message"
```

#### `reply --token <capability-token> --to-message <message-id> --body <text>`

Reply to a message. The reply is automatically sent to the original sender and all original recipients (except yourself).

```bash
agent-inbox reply --token "$BOB_TOKEN" --to-message 1 --body "Hi Alice!"
```

#### `inbox --token <capability-token> [--unread-only]`

List messages in the inbox authorized by the capability token.

```bash
agent-inbox inbox --token "$ALICE_TOKEN"
agent-inbox inbox --token "$ALICE_TOKEN" --unread-only
agent-inbox inbox --token "$ALICE_TOKEN" --format json
```

#### `read <message-id> --token <capability-token>`

Read a specific message and mark it as read for the authorized agent.

```bash
agent-inbox read 1 --token "$ALICE_TOKEN"
```

## Output Formats

- **plain**: Human-readable tabular output (default)
- **json**: JSON array or object
- **ndjson**: Newline-delimited JSON (one object per line); useful for streaming/piping

## Database

The SQLite database is created automatically on first use. Schema:

- **agents**: Registered agents with optional display names, capability token hashes, token creation timestamps, and soft-delete support
- **messages**: Messages with sender, subject, body, and optional reply threading
- **message_recipients**: Many-to-many join table tracking delivery and read status

This schema change currently assumes a fresh database. Database migration and backward compatibility for older databases are not handled yet.

## Security and Trust Model

- `agent-inbox` is designed for mutually trusted local processes on the same machine.
- Capability tokens authorize actions like sending, replying, reading, and viewing inbox contents.
- Agent IDs identify message addresses, but do not authorize actions by themselves.
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
