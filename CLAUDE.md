# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build solution
dotnet build GraphLedger.sln

# Run tests
dotnet test

# Run CLI
dotnet run --project src/GraphLedger.Cli -- <command>

# Run WebUI
dotnet run --project src/GraphLedger.WebUI

# Run background service
dotnet run --project src/GraphLedger.Service
```

## CLI Commands

```bash
# Configuration
graphledger config set <key> <value>   # e.g., Azure:TenantId, Azure:ClientId
graphledger config get [key]

# Workloads (explore UTCM resource types)
graphledger workload list              # List all supported workloads
graphledger workload resources <name>  # List resource types for a workload

# Snapshots
graphledger snapshot take [--workloads Entra,Intune] [--resource-types <types>]
graphledger snapshot list [--limit <n>] [--workload <name>] [--resource-type <type>]
graphledger snapshot diff <id1> <id2> [--json]
graphledger snapshot export <id> --output <file>
graphledger snapshot show <id>

# Monitors (UTCM drift detection)
graphledger monitor create <name> --workloads Entra,Intune [--frequency 24]
graphledger monitor list [--local]
graphledger monitor show <id>
graphledger monitor delete <id> [--force]

# Drifts (view detected configuration changes)
graphledger drift list [--monitor <id>] [--status active|fixed]
graphledger drift show <id> [--json]
```

## Architecture

GraphLedger is an M365 configuration management tool using Microsoft Graph UTCM (Unified Tenant Configuration Management) APIs.

**Project Structure:**
- `GraphLedger.Core` - Shared library: domain models, EF Core storage, Graph client, diff engine
- `GraphLedger.Cli` - CLI using System.CommandLine
- `GraphLedger.Service` - Background worker with Quartz.NET scheduler
- `GraphLedger.WebUI` - Blazor Server dashboard

**Key Patterns:**
- CLI and WebUI share config from `~/.graphledger/config.json`
- Database path is configurable via `Storage.DatabasePath` in user config
- `ISnapshotRepository` abstracts all snapshot CRUD operations
- `IDiffEngine` with `JsonDiffEngine` implementation for comparing snapshots
- `IUtcmClient` wraps UTCM API calls via HttpClient with bearer token auth
- `UtcmResourceTypeRegistry` maps workloads to UTCM resource types

**UTCM Workloads:**
- `Entra` - Conditional Access, Auth Methods, Cross-Tenant Access, Groups, Apps
- `Exchange` - Accepted Domains, Anti-Phish, DKIM, Transport Rules
- `Intune` - Device Compliance, App Protection policies
- `Teams` - Meeting, Messaging, Calling policies
- `Security` - DLP, Retention policies

**Data Flow:**
1. `UtcmClient` creates async snapshot jobs via UTCM API
2. Polls for job completion, then fetches snapshot resources
3. Snapshots stored as JSON in SQLite via EF Core
4. UTCM server-side drift detection via monitors
5. `JsonDiffEngine` for local snapshot comparison
6. WebUI displays snapshots and diffs via `IDbContextFactory<GraphLedgerDbContext>`

## Configuration

User config stored at `~/.graphledger/config.json`:
```json
{
  "Azure": {
    "TenantId": "",
    "ClientId": "",
    "ClientSecret": ""
  },
  "Storage": {
    "DatabasePath": "data/graphledger.db"
  }
}
```

Required Azure AD app permission: `ConfigurationMonitoring.ReadWrite.All`

This single permission replaces the previous granular permissions and enables:
- Taking configuration snapshots via UTCM
- Creating drift monitors
- Viewing drift detection results
