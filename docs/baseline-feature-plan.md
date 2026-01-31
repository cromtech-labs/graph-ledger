# Baseline & Compliance Monitoring Feature Plan

## Overview

Add the ability to define a "baseline" (approved/compliant configuration state) and monitor for drift from that baseline. Users should be able to:

1. Mark any snapshot as a baseline
2. Configure which properties to monitor vs ignore
3. See a compliance dashboard showing drift from baseline
4. Receive alerts when configuration drifts
5. Export baseline as Infrastructure-as-Code (Terraform, ARM, PowerShell)

---

## Phase 1: Database & Core Models

### New Entities

#### `Baseline`
```csharp
public class Baseline
{
    public Guid Id { get; set; }
    public string Name { get; set; }                    // "Production CA Policies"
    public string? Description { get; set; }
    public Guid SnapshotId { get; set; }                // Reference to the baseline snapshot
    public Snapshot Snapshot { get; set; }
    public string ResourceType { get; set; }            // "microsoft.entra.conditionalaccesspolicy"
    public string? ResourceDisplayName { get; set; }    // Optional: specific resource only
    public DateTime CreatedAt { get; set; }
    public DateTime? LastCheckedAt { get; set; }
    public BaselineStatus Status { get; set; }          // Compliant, Drifted, Unknown
    public string IgnoreRulesJson { get; set; }         // JSON array of ignore rules
}

public enum BaselineStatus
{
    Unknown,
    Compliant,
    Drifted
}
```

#### `BaselineIgnoreRule`
```csharp
public class BaselineIgnoreRule
{
    public string PropertyPath { get; set; }            // "lastModifiedDateTime" or "$.properties.createdDateTime"
    public IgnoreRuleType Type { get; set; }            // Exact, Prefix, Regex
    public string? Reason { get; set; }                 // "Timestamp always changes"
}

public enum IgnoreRuleType
{
    Exact,      // Ignore exact property path
    Prefix,     // Ignore all paths starting with prefix
    Regex       // Ignore paths matching regex
}
```

#### `BaselineDrift`
```csharp
public class BaselineDrift
{
    public Guid Id { get; set; }
    public Guid BaselineId { get; set; }
    public Baseline Baseline { get; set; }
    public Guid CurrentSnapshotId { get; set; }
    public Snapshot CurrentSnapshot { get; set; }
    public DateTime DetectedAt { get; set; }
    public string DiffJson { get; set; }                // JSON diff result
    public int ChangeCount { get; set; }
    public DriftStatus Status { get; set; }             // Active, Acknowledged, Resolved
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? Notes { get; set; }
}

public enum DriftStatus
{
    Active,         // Drift detected, needs attention
    Acknowledged,   // User acknowledged, will fix later
    Resolved        // Configuration restored to baseline
}
```

### Database Changes

Update `GraphLedgerDbContext`:
```csharp
public DbSet<Baseline> Baselines { get; set; }
public DbSet<BaselineDrift> BaselineDrifts { get; set; }
```

Add migrations for new tables with proper indexes.

---

## Phase 2: Core Services

### `IBaselineService`
```csharp
public interface IBaselineService
{
    // Baseline management
    Task<Baseline> CreateBaselineAsync(Guid snapshotId, string name, string? description = null);
    Task<Baseline?> GetBaselineAsync(Guid id);
    Task<IReadOnlyList<Baseline>> ListBaselinesAsync();
    Task DeleteBaselineAsync(Guid id);

    // Ignore rules
    Task AddIgnoreRuleAsync(Guid baselineId, BaselineIgnoreRule rule);
    Task RemoveIgnoreRuleAsync(Guid baselineId, string propertyPath);
    Task<IReadOnlyList<BaselineIgnoreRule>> GetIgnoreRulesAsync(Guid baselineId);

    // Compliance checking
    Task<BaselineDrift?> CheckComplianceAsync(Guid baselineId, Guid currentSnapshotId);
    Task<IReadOnlyList<BaselineDrift>> GetActiveDriftsAsync();

    // Drift management
    Task AcknowledgeDriftAsync(Guid driftId, string acknowledgedBy, string? notes = null);
    Task ResolveDriftAsync(Guid driftId);
}
```

### `BaselineComparer`
```csharp
public class BaselineComparer
{
    private readonly IDiffEngine _diffEngine;

    public DiffResult Compare(Snapshot baseline, Snapshot current, IEnumerable<BaselineIgnoreRule> ignoreRules)
    {
        var rawDiff = _diffEngine.Compare(baseline, current);

        // Filter out ignored changes
        var filteredChanges = rawDiff.Changes
            .Where(change => !ShouldIgnore(change.Path, ignoreRules))
            .ToList();

        return new DiffResult
        {
            Changes = filteredChanges,
            HasChanges = filteredChanges.Any(),
            ChangeCount = filteredChanges.Count,
            RawDiffJson = rawDiff.RawDiffJson
        };
    }

    private bool ShouldIgnore(string path, IEnumerable<BaselineIgnoreRule> rules)
    {
        foreach (var rule in rules)
        {
            switch (rule.Type)
            {
                case IgnoreRuleType.Exact when path == rule.PropertyPath:
                case IgnoreRuleType.Prefix when path.StartsWith(rule.PropertyPath):
                case IgnoreRuleType.Regex when Regex.IsMatch(path, rule.PropertyPath):
                    return true;
            }
        }
        return false;
    }
}
```

---

## Phase 3: CLI Commands

### New Commands

```bash
# Baseline management
graphledger baseline create <snapshot-id> --name "CA Policies Baseline" [--description "..."]
graphledger baseline list
graphledger baseline show <baseline-id>
graphledger baseline delete <baseline-id>

# Ignore rules
graphledger baseline ignore <baseline-id> --path "lastModifiedDateTime" [--type exact|prefix|regex]
graphledger baseline ignore list <baseline-id>
graphledger baseline ignore remove <baseline-id> --path "lastModifiedDateTime"

# Compliance checking
graphledger baseline check <baseline-id>                    # Check against latest snapshot
graphledger baseline check <baseline-id> --snapshot <id>    # Check against specific snapshot

# Drift management
graphledger drift list [--baseline <id>] [--status active|acknowledged|resolved]
graphledger drift acknowledge <drift-id> --notes "Will fix in next sprint"
graphledger drift resolve <drift-id>
```

---

## Phase 4: WebUI - Baseline Management

### New Pages

#### `/baselines` - Baseline List
- Table of all baselines with status indicators
- Quick actions: Check, View, Delete
- Create baseline button

#### `/baselines/{id}` - Baseline Detail
- Baseline info (name, description, created date)
- Source snapshot details
- Ignore rules editor (add/remove rules)
- Current compliance status with diff viewer
- History of drift detections

#### `/baselines/create` - Create Baseline Wizard
1. Select snapshot to use as baseline
2. Enter name and description
3. Configure common ignore rules (timestamps, etc.)
4. Preview what will be monitored
5. Create baseline

### Dashboard Updates

#### Compliance Widget on Dashboard
```
┌─────────────────────────────┐
│  Compliance Status          │
│  ─────────────────────      │
│  ✅ 5 Compliant             │
│  ⚠️  2 Drifted              │
│  ❓ 1 Unknown               │
│                             │
│  [View All Baselines]       │
└─────────────────────────────┘
```

#### Active Drifts Alert
```
┌─────────────────────────────────────────────────────┐
│  ⚠️ Active Drift Detected                           │
│  ─────────────────────────────────────────────      │
│  CA Policy Baseline: 3 changes detected             │
│    - BlockLegacyAuth: State changed                 │
│    - MFA for Admins: IncludeRoles modified          │
│                                                     │
│  [View Details]  [Acknowledge]                      │
└─────────────────────────────────────────────────────┘
```

### Snapshot Page Updates

Add "Set as Baseline" button:
```html
<button class="btn btn-outline-success" @onclick="CreateBaseline">
    📌 Set as Baseline
</button>
```

---

## Phase 5: Scheduled Compliance Monitoring

### Background Service Integration

Update `SnapshotPollingJob` to check baselines after taking snapshots:

```csharp
public async Task Execute(IJobExecutionContext context)
{
    // ... existing snapshot logic ...

    // After taking snapshots, check baselines
    var baselines = await baselineService.ListBaselinesAsync();
    foreach (var baseline in baselines)
    {
        // Find latest snapshot for this resource type
        var latestSnapshot = await GetLatestSnapshot(baseline.ResourceType);
        if (latestSnapshot != null)
        {
            var drift = await baselineService.CheckComplianceAsync(baseline.Id, latestSnapshot.Id);
            if (drift != null && drift.Status == DriftStatus.Active)
            {
                _logger.LogWarning("Drift detected for baseline {Name}: {Changes} changes",
                    baseline.Name, drift.ChangeCount);

                // TODO: Send alert (email, webhook, etc.)
            }
        }
    }
}
```

### Alert Configuration

Add to `AppConfiguration`:
```csharp
public class AlertSettings
{
    public bool Enabled { get; set; } = false;
    public string? WebhookUrl { get; set; }              // Slack, Teams, etc.
    public string? EmailRecipients { get; set; }         // Comma-separated
    public AlertSeverity MinimumSeverity { get; set; } = AlertSeverity.Warning;
}
```

---

## Phase 6: Export as Infrastructure-as-Code

### Export Formats

#### PowerShell (Microsoft365DSC style)
```powershell
# Conditional Access Policy: BlockLegacyAuth
$params = @{
    DisplayName = "BlockLegacyAuth"
    State = "enabled"
    ClientAppTypes = @("exchangeActiveSync", "other")
    IncludeUsers = @("All")
    ExcludeUsers = @("breakglass@contoso.com")
    IncludeApplications = @("All")
    BuiltInControls = @("block")
}
New-MgIdentityConditionalAccessPolicy @params
```

#### Terraform
```hcl
resource "azuread_conditional_access_policy" "block_legacy_auth" {
  display_name = "BlockLegacyAuth"
  state        = "enabled"

  conditions {
    client_app_types = ["exchangeActiveSync", "other"]
    users {
      included_users = ["All"]
      excluded_users = ["breakglass@contoso.com"]
    }
    applications {
      included_applications = ["All"]
    }
  }

  grant_controls {
    built_in_controls = ["block"]
    operator          = "OR"
  }
}
```

#### ARM Template
```json
{
  "$schema": "...",
  "resources": [{
    "type": "Microsoft.AAD/conditionalAccessPolicies",
    "name": "BlockLegacyAuth",
    "properties": {
      "displayName": "BlockLegacyAuth",
      "state": "enabled",
      ...
    }
  }]
}
```

### CLI Commands

```bash
graphledger baseline export <baseline-id> --format powershell --output baseline.ps1
graphledger baseline export <baseline-id> --format terraform --output baseline.tf
graphledger baseline export <baseline-id> --format arm --output baseline.json
```

---

## Implementation Order

### MVP (Phase 1-3)
1. Database models and migrations
2. BaselineService implementation
3. CLI commands for baseline CRUD
4. Basic compliance checking

### WebUI Integration (Phase 4)
5. Baseline list/detail pages
6. "Set as Baseline" button on snapshots
7. Dashboard compliance widget
8. Ignore rules editor

### Automation (Phase 5)
9. Scheduled compliance checks
10. Alert configuration
11. Webhook/email notifications

### Export (Phase 6)
12. PowerShell export
13. Terraform export
14. ARM template export

---

## Default Ignore Rules

Suggest these by default when creating a baseline:

| Property Path | Reason |
|--------------|--------|
| `lastModifiedDateTime` | Timestamp always changes |
| `createdDateTime` | Not relevant for comparison |
| `Id` | Unique identifier, always different |
| `@odata.*` | OData metadata |

---

## Success Metrics

- User can create baseline in < 30 seconds
- Compliance check runs in < 5 seconds for 100 resources
- Dashboard shows compliance status at a glance
- Alerts delivered within 5 minutes of drift detection
