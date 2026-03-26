# agent-inbox

A CLI tool for inter-agent communication on a single machine, backed by a local SQLite database.

## Overview

`agent-inbox` allows AI agents (or any processes) to register themselves, send messages to each other, and read their inboxes — all via a simple command-line interface. Data is stored in a local SQLite database (`~/.agent-inbox/inbox.db` by default).

## Requirements

- .NET 10 SDK

## Build

```bash
dotnet build src/AgentInbox/AgentInbox.csproj
```

## Run Tests

```bash
dotnet test tests/AgentInbox.Tests/AgentInbox.Tests.csproj
```

## Usage

```
agent-inbox [--db-path <path>] [--format plain|json|ndjson] <command> [options]
```

### Global Options

| Option | Default | Description |
|--------|---------|-------------|
| `--db-path` | `~/.agent-inbox/inbox.db` | Path to the SQLite database file |
| `--format`, `-f` | `plain` | Output format: `plain`, `json`, or `ndjson` |

### Commands

#### `register <agent-id> [--display-name <name>]`
Register a new agent. If the agent was previously deregistered, it will be reactivated.

```bash
agent-inbox register my-agent --display-name "My Agent"
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

#### `send --from <agent-id> --to <agent-id[,agent-id,...]> --body <text> [--subject <text>]`
Send a message from one agent to one or more recipients.

```bash
agent-inbox send --from alice --to bob --subject "Hello" --body "Hi Bob!"
agent-inbox send --from alice --to bob,carol --body "Group message"
```

#### `reply --from <agent-id> --to-message <message-id> --body <text>`
Reply to a message. The reply is automatically sent to the original sender and all original recipients (except yourself).

```bash
agent-inbox reply --from bob --to-message 1 --body "Hi Alice!"
```

#### `inbox <agent-id> [--unread-only]`
List messages in an agent's inbox.

```bash
agent-inbox inbox alice
agent-inbox inbox alice --unread-only
agent-inbox inbox alice --format json
```

#### `read <message-id> --as <agent-id>`
Read a specific message and mark it as read for the specified agent.

```bash
agent-inbox read 1 --as alice
```

## Output Formats

- **plain**: Human-readable tabular output (default)
- **json**: JSON array or object
- **ndjson**: Newline-delimited JSON (one object per line); useful for streaming/piping

## Database

The SQLite database is created automatically on first use. Schema:

- **agents**: Registered agents with optional display names and soft-delete support
- **messages**: Messages with sender, subject, body, and optional reply threading
- **message_recipients**: Many-to-many join table tracking delivery and read status

## Example Workflow

```bash
# Register agents
agent-inbox register alice --display-name "Alice"
agent-inbox register bob --display-name "Bob"

# List agents
agent-inbox agents

# Send a message
agent-inbox send --from alice --to bob --subject "Hello" --body "Hi Bob, how are you?"

# Check inbox
agent-inbox inbox bob

# Read a message (marks as read)
agent-inbox read 1 --as bob

# Reply
agent-inbox reply --from bob --to-message 1 --body "Hi Alice! I'm doing great."

# Check Alice's inbox for the reply
agent-inbox inbox alice
```
