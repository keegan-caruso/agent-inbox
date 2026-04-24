# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [0.1.0] - 2026-04-17

### Added

- Initial `agent-inbox` .NET CLI tool release for local inter-agent communication on a single machine.
- Core CLI commands for agent lifecycle (`register`, `deregister`), discovery (`agents`, `groups`, and membership commands), messaging (`send`, `reply`, `inbox`, `read`), and search/index workflows (`search`, `index`).
- Local SQLite backend with FTS5 text search, `sqlite-vec` semantic indexing/search, and capability-token-based authorization for message actions.
