# Resource History & Smart Comparison Feature Plan

## Problem Statement

The current Diff Viewer is not useful because:

1. **Mixed Resources**: The snapshot dropdown shows ALL snapshots across ALL resources in one flat list
2. **Meaningless Comparisons**: Comparing "Access Package A" to "Access Package B" makes no sense - they're different objects
3. **No Change Tracking**: Users can't see when a resource last changed without manually comparing snapshots
4. **Wasted Effort**: Users might compare two snapshots only to find they're identical

### What Users Actually Need

- Compare the **same resource** across different points in time
- Know **when** a resource last changed
- See a **history/timeline** of changes for a specific resource
- Avoid comparing snapshots that have no differences
- Quick navigation: "Show me what changed in the last week"

---

## Solution Overview

Introduce **Resource Tracking** as a first-class concept:

1. **Resource Entity**: Track unique resources across snapshots using their Graph API ID
2. **Change Detection**: Compute configuration hashes to instantly detect changes
3. **Resource Browser**: New UI to browse resources and see change history
4. **Timeline View**: Visualize a resource's history with change highlights
5. **Smart Diff Viewer**: Filter by resource type → resource → snapshots
6. No backwards compatability: This is under development and only I use it - do not put in workarounds for backwards compatibility. If a feature has to change, please change it (e.g. completely remove the old capability even if it will break something)

---

## Data Model Changes

### New Table: `Resources`

Tracks unique resources across all snapshots.

```csharp
// src/GraphLedger.Core/Models/Resource.cs
public class Resource
{
    public Guid Id { get; set; }

    /// <summary>
    /// The stable identifier from Graph API (e.g., Conditional Access Policy ID).
    /// Combined with ResourceType, uniquely identifies a resource.
    /// </summary>
    public string ExternalId { get; set; } = "";

    /// <summary>
    /// UTCM resource type (e.g., "microsoft.entra.conditionalAccessPolicy")
    /// </summary>
    public string ResourceType { get; set; } = "";

    /// <summary>
    /// Human-readable name (may change over time, updated on each snapshot)
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Workload category (Entra, Intune, Exchange, etc.)
    /// </summary>
    public string Workload { get; set; } = "";

    /// <summary>
    /// When this resource was first seen
    /// </summary>
    public DateTime FirstSeenAt { get; set; }

    /// <summary>
    /// When the resource configuration last changed (not just last snapshot)
    /// </summary>
    public DateTime? LastChangedAt { get; set; }

    /// <summary>
    /// Total number of snapshots for this resource
    /// </summary>
    public int SnapshotCount { get; set; }

    /// <summary>
    /// Number of snapshots where configuration changed from previous
    /// </summary>
    public int ChangeCount { get; set; }

    /// <summary>
    /// Reference to the most recent snapshot
    /// </summary>
    public Guid? LatestSnapshotId { get; set; }
    public Snapshot? LatestSnapshot { get; set; }

    /// <summary>
    /// All snapshots for this resource
    /// </summary>
    public ICollection<Snapshot> Snapshots { get; set; } = new List<Snapshot>();
}
```

### Extended: `Snapshot`

Add fields to link snapshots to resources and track changes.

```csharp
// Add to existing Snapshot.cs
public class Snapshot
{
    // ... existing fields ...

    /// <summary>
    /// Link to the tracked resource (nullable for migration)
    /// </summary>
    public Guid? ResourceId { get; set; }
    public Resource? Resource { get; set; }

    /// <summary>
    /// The Graph API ID extracted from the configuration JSON.
    /// Used to match snapshots to resources.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// SHA256 hash of normalized ConfigurationJson for quick equality checks.
    /// Two snapshots with the same hash are identical.
    /// </summary>
    public string? ConfigurationHash { get; set; }

    /// <summary>
    /// True if this snapshot's configuration differs from the previous snapshot
    /// of the same resource. Null for first snapshot or unknown.
    /// </summary>
    public bool? HasChangesFromPrevious { get; set; }

    /// <summary>
    /// Reference to the previous snapshot of the same resource (if any)
    /// </summary>
    public Guid? PreviousSnapshotId { get; set; }
    public Snapshot? PreviousSnapshot { get; set; }
}
```

### Database Context Updates

```csharp
// src/GraphLedger.Core/Storage/GraphLedgerDbContext.cs
public DbSet<Resource> Resources => Set<Resource>();

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Resource configuration
    modelBuilder.Entity<Resource>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.ExternalId, e.ResourceType }).IsUnique();
        entity.HasIndex(e => e.ResourceType);
        entity.HasIndex(e => e.Workload);
        entity.HasIndex(e => e.LastChangedAt);

        entity.HasOne(e => e.LatestSnapshot)
            .WithMany()
            .HasForeignKey(e => e.LatestSnapshotId)
            .OnDelete(DeleteBehavior.SetNull);
    });

    // Snapshot updates
    modelBuilder.Entity<Snapshot>(entity =>
    {
        entity.HasOne(e => e.Resource)
            .WithMany(r => r.Snapshots)
            .HasForeignKey(e => e.ResourceId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasOne(e => e.PreviousSnapshot)
            .WithMany()
            .HasForeignKey(e => e.PreviousSnapshotId)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.ResourceId);
        entity.HasIndex(e => e.ExternalId);
        entity.HasIndex(e => e.ConfigurationHash);
    });
}
```

---

## Core Services

### Configuration Hash Service

Computes a stable hash for configuration comparison.

```csharp
// src/GraphLedger.Core/Services/IConfigurationHashService.cs
public interface IConfigurationHashService
{
    /// <summary>
    /// Computes SHA256 hash of normalized JSON configuration.
    /// Normalization: sorted keys, no whitespace, consistent formatting.
    /// </summary>
    string ComputeHash(string configurationJson);

    /// <summary>
    /// Extracts the external ID from configuration JSON.
    /// Looks for "id" field at root level.
    /// </summary>
    string? ExtractExternalId(string configurationJson);
}

// src/GraphLedger.Core/Services/ConfigurationHashService.cs
public class ConfigurationHashService : IConfigurationHashService
{
    public string ComputeHash(string configurationJson)
    {
        // 1. Parse JSON
        // 2. Normalize (sort keys recursively, remove formatting)
        // 3. Serialize to canonical form
        // 4. Compute SHA256
        // 5. Return as hex string
    }

    public string? ExtractExternalId(string configurationJson)
    {
        // Parse JSON and look for "id" field
        // Handle nested structures if needed
    }
}
```

### Resource Tracking Service

Manages resource lifecycle and change detection.

```csharp
// src/GraphLedger.Core/Services/IResourceTrackingService.cs
public interface IResourceTrackingService
{
    /// <summary>
    /// Processes a new snapshot: links to resource, computes hash, detects changes.
    /// Creates new Resource record if this is the first snapshot for this resource.
    /// </summary>
    Task<Snapshot> ProcessSnapshotAsync(Snapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Gets all resources, optionally filtered.
    /// </summary>
    Task<IReadOnlyList<Resource>> GetResourcesAsync(
        string? workload = null,
        string? resourceType = null,
        bool? hasRecentChanges = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the snapshot history for a specific resource.
    /// </summary>
    Task<IReadOnlyList<Snapshot>> GetResourceHistoryAsync(
        Guid resourceId,
        bool includeUnchanged = true,
        CancellationToken ct = default);

    /// <summary>
    /// Gets resources that changed within a time period.
    /// </summary>
    Task<IReadOnlyList<Resource>> GetRecentlyChangedResourcesAsync(
        TimeSpan period,
        CancellationToken ct = default);

    /// <summary>
    /// Backfills resource tracking for existing snapshots (migration).
    /// </summary>
    Task BackfillResourceTrackingAsync(IProgress<int>? progress = null, CancellationToken ct = default);
}
```

### Implementation Notes

```csharp
// src/GraphLedger.Core/Services/ResourceTrackingService.cs
public class ResourceTrackingService : IResourceTrackingService
{
    public async Task<Snapshot> ProcessSnapshotAsync(Snapshot snapshot, CancellationToken ct)
    {
        // 1. Extract external ID from configuration JSON
        var externalId = _hashService.ExtractExternalId(snapshot.ConfigurationJson);
        snapshot.ExternalId = externalId;

        // 2. Compute configuration hash
        snapshot.ConfigurationHash = _hashService.ComputeHash(snapshot.ConfigurationJson);

        // 3. Find or create Resource record
        var resource = await FindOrCreateResourceAsync(
            externalId,
            snapshot.UtcmResourceType,
            snapshot.ResourceDisplayName,
            snapshot.Workload,
            ct);

        snapshot.ResourceId = resource.Id;

        // 4. Find previous snapshot of this resource
        var previousSnapshot = await _context.Snapshots
            .Where(s => s.ResourceId == resource.Id && s.Id != snapshot.Id)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (previousSnapshot != null)
        {
            snapshot.PreviousSnapshotId = previousSnapshot.Id;

            // 5. Compare hashes to detect changes
            snapshot.HasChangesFromPrevious =
                snapshot.ConfigurationHash != previousSnapshot.ConfigurationHash;

            // 6. Update resource metadata
            if (snapshot.HasChangesFromPrevious == true)
            {
                resource.LastChangedAt = snapshot.CreatedAt;
                resource.ChangeCount++;
            }
        }
        else
        {
            // First snapshot for this resource
            snapshot.HasChangesFromPrevious = null; // Unknown (no previous)
            resource.FirstSeenAt = snapshot.CreatedAt;
        }

        // 7. Update resource counters
        resource.SnapshotCount++;
        resource.LatestSnapshotId = snapshot.Id;
        resource.DisplayName = snapshot.ResourceDisplayName ?? resource.DisplayName;

        await _context.SaveChangesAsync(ct);
        return snapshot;
    }
}
```

---

## CLI Updates

### New Command: `resource`

```bash
# List all tracked resources
graphledger resource list [--workload <name>] [--type <resourceType>] [--changed-since <days>]

# Show resource details and history summary
graphledger resource show <resourceId>

# Show full history for a resource
graphledger resource history <resourceId> [--show-unchanged] [--limit <n>]

# Compare to previous snapshot (auto-selects)
graphledger resource diff <resourceId> [--from <snapshotId>]
```

### Updated `snapshot` Commands

```bash
# List now shows resource info and change status
graphledger snapshot list
# Output includes: ResourceName, HasChanges column

# Diff can auto-select previous snapshot
graphledger snapshot diff <snapshotId> --previous
# Automatically finds and compares to previous snapshot of same resource
```

### Migration Command

```bash
# Backfill resource tracking for existing snapshots
graphledger admin backfill-resources [--dry-run]
```

---

## WebUI Changes

### New Page: Resource Browser (`/resources`)

Main entry point for exploring resources.

```
┌─────────────────────────────────────────────────────────────────────┐
│ Resources                                          [Refresh]        │
├─────────────────────────────────────────────────────────────────────┤
│ Filter: [All Workloads ▼] [All Types ▼] [Changed in: 7 days ▼]     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│ ┌─ Entra ──────────────────────────────────────────────────────┐   │
│ │                                                               │   │
│ │ ConditionalAccessPolicy (12 resources)                       │   │
│ │ ┌───────────────────────────────────────────────────────────┐│   │
│ │ │ ● Block Legacy Authentication                             ││   │
│ │ │   Last changed: 2 days ago │ 5 snapshots │ 3 changes     ││   │
│ │ │   [View History] [Compare Latest]                         ││   │
│ │ ├───────────────────────────────────────────────────────────┤│   │
│ │ │ ● Require MFA for Administrators                          ││   │
│ │ │   Last changed: 5 days ago │ 8 snapshots │ 2 changes     ││   │
│ │ │   [View History] [Compare Latest]                         ││   │
│ │ ├───────────────────────────────────────────────────────────┤│   │
│ │ │ ○ Block High Risk Sign-ins                                ││   │
│ │ │   No changes │ 4 snapshots                                ││   │
│ │ │   [View History]                                          ││   │
│ │ └───────────────────────────────────────────────────────────┘│   │
│ │                                                               │   │
│ │ AuthenticationMethodPolicy (3 resources)                     │   │
│ │ └─ ...                                                       │   │
│ └───────────────────────────────────────────────────────────────┘   │
│                                                                     │
│ ┌─ Intune ─────────────────────────────────────────────────────┐   │
│ │ DeviceCompliancePolicy (8 resources)                         │   │
│ │ └─ ...                                                       │   │
│ └───────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────┘
```

**Legend:**
- ● = Has changes (shows last changed date)
- ○ = No changes detected (stable)

### New Page: Resource History (`/resources/{id}`)

Timeline view for a single resource.

```
┌─────────────────────────────────────────────────────────────────────┐
│ ← Back to Resources                                                 │
│                                                                     │
│ Block Legacy Authentication                                         │
│ ConditionalAccessPolicy │ Entra                                     │
│ External ID: 12345678-abcd-...                                      │
├─────────────────────────────────────────────────────────────────────┤
│ [Show All Snapshots] [Show Only Changes ●]                          │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│ Timeline                                                            │
│                                                                     │
│ ┌─ Jan 31, 2026 01:41 ─────────────────────────────── CHANGED ─┐   │
│ │ Snapshot: 5b6e713d                                           │   │
│ │                                                               │   │
│ │ Changes from previous:                                        │   │
│ │ ┌───────────────────────────────────────────────────────────┐│   │
│ │ │ + conditions.users.includeGroups: Added "Security Admins" ││   │
│ │ │ ~ grantControls.builtInControls: "mfa" → "compliantDevice"││   │
│ │ └───────────────────────────────────────────────────────────┘│   │
│ │                                                               │   │
│ │ [View Full Diff] [View Configuration] [Export]               │   │
│ └───────────────────────────────────────────────────────────────┘   │
│ │                                                                   │
│ ▼                                                                   │
│ ┌─ Jan 30, 2026 14:22 ─────────────────────────── NO CHANGES ──┐   │
│ │ Snapshot: a1b2c3d4                                           │   │
│ │ Configuration identical to previous snapshot                  │   │
│ │ [View Configuration]                                          │   │
│ └───────────────────────────────────────────────────────────────┘   │
│ │                                                                   │
│ ▼                                                                   │
│ ┌─ Jan 30, 2026 09:15 ─────────────────────────────── CHANGED ─┐   │
│ │ Snapshot: e5f6g7h8                                           │   │
│ │                                                               │   │
│ │ Changes from previous:                                        │   │
│ │ ┌───────────────────────────────────────────────────────────┐│   │
│ │ │ + state: "enabled" (was "disabled")                       ││   │
│ │ └───────────────────────────────────────────────────────────┘│   │
│ │                                                               │   │
│ │ [View Full Diff] [View Configuration] [Export]               │   │
│ └───────────────────────────────────────────────────────────────┘   │
│ │                                                                   │
│ ▼                                                                   │
│ ┌─ Jan 29, 2026 18:00 ──────────────────────── INITIAL ────────┐   │
│ │ Snapshot: i9j0k1l2                                           │   │
│ │ First snapshot captured                                       │   │
│ │ [View Configuration] [Export]                                 │   │
│ └───────────────────────────────────────────────────────────────┘   │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Updated: Diff Viewer (`/diff`)

Add resource filtering to make comparisons meaningful.

```
┌─────────────────────────────────────────────────────────────────────┐
│ Diff Viewer                                                         │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│ 1. Select Resource Type                                             │
│    [ConditionalAccessPolicy                                    ▼]   │
│                                                                     │
│ 2. Select Resource                                                  │
│    [Block Legacy Authentication                                ▼]   │
│    ℹ 5 snapshots available, 3 with changes                         │
│                                                                     │
│ 3. Select Snapshots to Compare                                      │
│                                                                     │
│    Left (Older)                    Right (Newer)                    │
│    [Jan 30 09:15 - CHANGED   ▼]   [Jan 31 01:41 - CHANGED   ▼]    │
│                                                                     │
│    [Compare]  [← Prev Change]  [Next Change →]  [Latest vs Prev]   │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│ ──── OR compare any two snapshots directly: ────                    │
│                                                                     │
│ [Advanced: Compare arbitrary snapshots...]                          │
└─────────────────────────────────────────────────────────────────────┘
```

**Dropdown improvements:**
- Show change status in dropdown: `Jan 30 09:15 - CHANGED` vs `Jan 30 14:22 - no changes`
- Pre-select most recent two changed snapshots
- Quick buttons: "Latest vs Previous", "Next Change", "Prev Change"

### Updated: Dashboard (`/`)

Add "Recent Changes" section.

```
┌─────────────────────────────────────────────────────────────────────┐
│ Dashboard                                                           │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐                 │
│ │ 156          │ │ 45           │ │ 12           │                 │
│ │ Total        │ │ Resources    │ │ Changed      │                 │
│ │ Snapshots    │ │ Tracked      │ │ This Week    │                 │
│ └──────────────┘ └──────────────┘ └──────────────┘                 │
│                                                                     │
│ Recent Changes                                     [View All →]     │
│ ┌───────────────────────────────────────────────────────────────┐  │
│ │ ● Block Legacy Auth (ConditionalAccessPolicy)                 │  │
│ │   Changed 2 hours ago                          [View Diff]    │  │
│ ├───────────────────────────────────────────────────────────────┤  │
│ │ ● iOS Compliance Policy (DeviceCompliancePolicy)              │  │
│ │   Changed 5 hours ago                          [View Diff]    │  │
│ ├───────────────────────────────────────────────────────────────┤  │
│ │ ● Transport Rule: Block External Forwarding                   │  │
│ │   Changed 1 day ago                            [View Diff]    │  │
│ └───────────────────────────────────────────────────────────────┘  │
│                                                                     │
│ Resources with Most Changes                                         │
│ ┌───────────────────────────────────────────────────────────────┐  │
│ │ 1. Cloud Privileged Access (AccessPackage) - 8 changes       │  │
│ │ 2. Require MFA for Admins (ConditionalAccessPolicy) - 5      │  │
│ │ 3. Windows 10 Compliance (DeviceCompliancePolicy) - 4        │  │
│ └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
```

### Updated: Snapshots List (`/snapshots`)

Add change indicator and resource grouping option.

```
┌─────────────────────────────────────────────────────────────────────┐
│ Snapshots                                                           │
├─────────────────────────────────────────────────────────────────────┤
│ View: [List ●] [Grouped by Resource]                               │
│ Filter: [All Workloads ▼] [Show: All ▼ / Changes Only]             │
├─────────────────────────────────────────────────────────────────────┤
│ ID       │ Resource                  │ Type    │ Date     │ Status │
│──────────┼───────────────────────────┼─────────┼──────────┼────────│
│ 5b6e713d │ Block Legacy Auth         │ CA      │ Jan 31   │ ● CHG  │
│ a1b2c3d4 │ Block Legacy Auth         │ CA      │ Jan 30   │ ○      │
│ 21acd489 │ General Access Package    │ AP      │ Jan 31   │ ○      │
│ fc420137 │ Entra ID Roles Package    │ AP      │ Jan 31   │ ● CHG  │
└─────────────────────────────────────────────────────────────────────┘

Legend: ● CHG = Changed from previous │ ○ = No changes
```

---

## Navigation Updates

### Sidebar

```
GraphLedger
├── Dashboard
├── Resources      ← NEW
│   └── (resource history pages)
├── Snapshots
├── Diff Viewer
└── Settings
```

### Breadcrumbs

- Resources → ConditionalAccessPolicy → Block Legacy Auth → Snapshot 5b6e713d
- Allows easy navigation up the hierarchy

---

## Implementation Phases

### Phase 1: Data Model & Migration
**Files to modify/create:**
- `src/GraphLedger.Core/Models/Resource.cs` (create)
- `src/GraphLedger.Core/Models/Snapshot.cs` (extend)
- `src/GraphLedger.Core/Storage/GraphLedgerDbContext.cs` (update)
- `src/GraphLedger.Core/Migrations/` (new migration)

**Tasks:**
1. Create Resource model
2. Add new fields to Snapshot
3. Create EF Core migration
4. Test migration on existing database

### Phase 2: Core Services
**Files to create:**
- `src/GraphLedger.Core/Services/IConfigurationHashService.cs`
- `src/GraphLedger.Core/Services/ConfigurationHashService.cs`
- `src/GraphLedger.Core/Services/IResourceTrackingService.cs`
- `src/GraphLedger.Core/Services/ResourceTrackingService.cs`

**Tasks:**
1. Implement hash computation (SHA256 of normalized JSON)
2. Implement external ID extraction from JSON
3. Implement resource tracking on snapshot save
4. Add change detection logic

### Phase 3: Backfill Migration
**Files to modify:**
- `src/GraphLedger.Cli/Commands/AdminCommands.cs` (create or extend)

**Tasks:**
1. Create `backfill-resources` command
2. Process existing snapshots to populate Resource table
3. Compute hashes and detect historical changes
4. Add progress reporting

### Phase 4: CLI Updates
**Files to modify:**
- `src/GraphLedger.Cli/Commands/ResourceCommands.cs` (create)
- `src/GraphLedger.Cli/Commands/SnapshotCommands.cs` (update)
- `src/GraphLedger.Cli/Program.cs` (register commands)

**Tasks:**
1. Add `resource list/show/history/diff` commands
2. Update `snapshot list` to show change status
3. Add `snapshot diff --previous` option

### Phase 5: WebUI - Resource Browser
**Files to create:**
- `src/GraphLedger.WebUI/Pages/Resources.razor`

**Tasks:**
1. Create resource list page
2. Add filtering by workload/type
3. Add "changed in last N days" filter
4. Show change counts and last changed dates

### Phase 6: WebUI - Resource History
**Files to create:**
- `src/GraphLedger.WebUI/Pages/ResourceHistory.razor`
- `src/GraphLedger.WebUI/Components/TimelineEntry.razor`

**Tasks:**
1. Create timeline view for single resource
2. Show inline change summaries
3. Add "show only changes" toggle
4. Link to full diff viewer

### Phase 7: WebUI - Diff Viewer Improvements
**Files to modify:**
- `src/GraphLedger.WebUI/Pages/DiffViewer.razor`

**Tasks:**
1. Add resource type filter
2. Add resource filter (cascading)
3. Show change status in snapshot dropdowns
4. Add quick navigation buttons
5. Pre-select meaningful default snapshots

### Phase 8: WebUI - Dashboard Updates
**Files to modify:**
- `src/GraphLedger.WebUI/Pages/Dashboard.razor`

**Tasks:**
1. Add "Resources Tracked" stat
2. Add "Changed This Week" stat
3. Add "Recent Changes" section
4. Add "Most Changed Resources" section

### Phase 9: WebUI - Navigation
**Files to modify:**
- `src/GraphLedger.WebUI/Shared/NavMenu.razor`
- Various pages (add breadcrumbs)

**Tasks:**
1. Add Resources link to nav
2. Implement breadcrumb component
3. Add navigation between related pages

---

## Testing Plan

### Unit Tests
- `ConfigurationHashService`: Hash stability, normalization
- `ResourceTrackingService`: Resource creation, change detection
- Edge cases: null IDs, empty JSON, malformed data

### Integration Tests
- Snapshot save flow with resource tracking
- Backfill migration on test data
- Query performance with many resources/snapshots

### Manual Testing
- Create multiple snapshots of same resource
- Verify change detection works
- Test all WebUI pages and navigation
- Verify filters work correctly

---

## Performance Considerations

1. **Hash computation**: Run async, cache in database
2. **Resource queries**: Add indexes on frequently filtered columns
3. **Timeline rendering**: Paginate for resources with many snapshots
4. **Backfill**: Process in batches with progress reporting

---

## Future Enhancements (Out of Scope)

1. **Change notifications**: Alert when specific resources change
2. **Change subscriptions**: Watch list of resources
3. **Change reports**: Weekly email digest of changes
4. **API**: REST endpoints for resource/change queries
5. **Comparison templates**: Save and reuse comparison configurations
