# agent-inbox

A CLI tool for inter-agent communication on a single machine, backed by a local SQLite database.

## Overview

`agent-inbox` allows AI agents (or any processes) to register themselves, send messages to each other, and read their inboxes — all via a simple command-line interface. Data is stored in a local SQLite database (`~/.agent-inbox/inbox.db` by default).

The system is intended for mutually trusted local processes. Capability tokens authorize message actions; agent IDs are addresses, not proof of authority. Agent discovery through `agent-inbox agents` is intentionally allowed.

Groups are named recipient sets with persistent membership. Groups are reusable send targets and are expanded to active member agents at send time. They are not shared inboxes or special conversation threads.

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
- `send`, `reply`, `read`, `inbox`, `search`, and `index` require a capability token.
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

#### `group-create <group-id>`

Create a group.

```bash
agent-inbox group-create engineering
```

#### `group-delete <group-id>`

Delete a group.

```bash
agent-inbox group-delete engineering
```

#### `groups`

List all active groups.

```bash
agent-inbox groups
agent-inbox groups --format json
```

#### `group-add-member <group-id> <agent-id>`

Add an agent to a group.

```bash
agent-inbox group-add-member engineering alice
```

#### `group-remove-member <group-id> <agent-id>`

Remove an agent from a group.

```bash
agent-inbox group-remove-member engineering alice
```

#### `group-members <group-id>`

List members of a group.

```bash
agent-inbox group-members engineering
agent-inbox group-members engineering --format json
```

#### `send --token <capability-token> --to <recipient[,recipient,...]> --body <text> [--subject <text>]`

Send a message from the agent authorized by the capability token to one or more recipients. A recipient can be either:

- an agent ID (for direct delivery)
- `group:<group-id>` (expanded to active member agents at send time)

```bash
agent-inbox send --token "$ALICE_TOKEN" --to bob --subject "Hello" --body "Hi Bob!"
agent-inbox send --token "$ALICE_TOKEN" --to bob,carol --body "Group message"
agent-inbox send --token "$ALICE_TOKEN" --to group:engineering --body "Deploy at 5"
agent-inbox send --token "$ALICE_TOKEN" --to bob,group:engineering --body "Please review"
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

#### `search --token <capability-token> --query <text> [--mode text|semantic] [--embedding <json>] [--limit <n>]`

Search inbox messages using full-text (FTS5) or semantic (vector) search, scoped to messages the authenticated agent is a recipient of.

| Option | Default | Description |
|--------|---------|-------------|
| `--query` | (required) | Search query text |
| `--mode` | `text` | Search mode: `text` (FTS5 BM25) or `semantic` (vector similarity) |
| `--embedding` | — | Pre-computed query embedding as a JSON float array (384 dimensions). Overrides built-in generation in semantic mode. |
| `--limit` | `10` | Maximum number of results |

```bash
# Full-text search (FTS5, no extra setup needed)
agent-inbox search --token "$BOB_TOKEN" --query "deployment"
agent-inbox search --token "$BOB_TOKEN" --query "deployment timeout" --limit 5

# Semantic search using built-in character n-gram embeddings
agent-inbox search --token "$BOB_TOKEN" --mode semantic --query "production issues"

# Semantic search with a pre-computed embedding (e.g., from Ollama or OpenAI)
agent-inbox search --token "$BOB_TOKEN" --mode semantic --embedding "[0.12, -0.05, ...]"
```

Semantic search requires messages to be indexed first with the `index` command. It also requires the `sqlite-vec` extension, which is bundled with the binary — it is enabled automatically when available.

#### `index <message-id> --token <capability-token> [--embedding <json>]`

Store or update the embedding for a message to enable semantic search. The caller must be the sender or a recipient of the message.

```bash
# Index using built-in character n-gram embeddings (no external tools needed)
agent-inbox index 1 --token "$BOB_TOKEN"

# Index with a pre-computed embedding from an external model
agent-inbox index 1 --token "$BOB_TOKEN" --embedding "[0.12, -0.05, ...]"
```

**About embeddings**: The built-in character n-gram embedding generator produces 384-dimensional vectors that support basic keyword-level similarity. For higher-quality semantic search, provide pre-computed embeddings from an external model:

- **Ollama** (`nomic-embed-text` produces 768-dim; use a 384-dim model like `all-minilm`):
  ```bash
  EMB=$(curl -s http://localhost:11434/api/embeddings -d '{"model":"all-minilm","prompt":"deployment failed"}' | jq -c .embedding)
  agent-inbox index 1 --token "$BOB_TOKEN" --embedding "$EMB"
  ```
- **OpenAI** (`text-embedding-3-small` with dimensions=384):
  ```bash
  EMB=$(curl -s https://api.openai.com/v1/embeddings \
    -H "Authorization: Bearer $OPENAI_API_KEY" \
    -d '{"model":"text-embedding-3-small","input":"deployment failed","dimensions":384}' | jq -c '.data[0].embedding')
  agent-inbox index 1 --token "$BOB_TOKEN" --embedding "$EMB"
  ```

> **Note**: All embeddings must be exactly 384 dimensions to match the schema. When using external models, choose a model or truncate to 384 dimensions.

## Output Formats

- **plain**: Human-readable tabular output (default)
- **json**: JSON array or object
- **ndjson**: Newline-delimited JSON (one object per line); useful for streaming/piping

## Database

The SQLite database is created automatically on first use. Schema:

- **agents**: Registered agents with optional display names, capability token hashes, token creation timestamps, and soft-delete support
- **messages**: Messages with sender, subject, body, and optional reply threading
- **message_recipients**: Many-to-many join table tracking delivery and read status
- **groups**: Named recipient sets (soft-delete supported)
- **group_members**: Group membership edges between groups and agents
- **messages_fts**: FTS5 virtual table for full-text search (always present; mirrors `messages`)
- **message_embeddings**: sqlite-vec `vec0` virtual table for semantic vector search (present when the sqlite-vec extension loads successfully; 384-dimensional float vectors)

This schema change currently assumes a fresh database. Database migration and backward compatibility for older databases are not handled yet.

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
