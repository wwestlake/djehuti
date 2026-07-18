# S3 Migration Infrastructure & Plan

## Overview

Move large static assets (graphics, PDFs, large files) from kwestkarz server to AWS S3 to free disk space and improve delivery performance.

**Current situation:**
- Server: 96% capacity (317 MB free)
- Model files freed: ~256 MB (Qwen2.5 → S3)
- Next: Graphics, assets, logs, old backups

## Strategy: "Build Plumbing First, Migrate Later"

1. **Phase 1: Infrastructure** — Set up S3 access layer and CDN
2. **Phase 2: Organize** — Plan folder structure and categorization
3. **Phase 3: Migrate** — Move files off server in batches
4. **Phase 4: Verify** — Ensure nothing is lost, all links work

## Phase 1: S3 Infrastructure

### Bucket Organization

```
us-east-1-886110331954-us-east-2-an/
├── models/
│   └── qwen-2.5-0.5b/           [265 MB] Already migrated
├── assets/
│   ├── learn/
│   │   ├── images/
│   │   ├── icons/
│   │   └── media/
│   ├── dashboard/
│   │   └── images/
│   └── common/
├── backups/
│   └── credentials-and-keys/     [~2 MB] Already backed up
├── archives/
│   └── old-logs/                 [To be migrated]
└── temp/
    └── build-artifacts/
```

### Asset CDN Configuration

**CloudFront distribution** (for fast delivery):
- Origin: S3 bucket
- Distribution: https://cdn.lagdaemon.com/ (or cdn.kwestkarz.com)
- Cache headers: Long TTL for static assets
- Invalidation: Clear cache when assets change

**S3 Access:**
- Public read for CDN (CloudFront can access)
- No direct public access (use CDN instead)
- Versioning enabled for backup recovery
- Lifecycle: Archive old versions to Glacier after 30 days

### Code Integration

**Asset helper class:**
```csharp
public static class AssetHelper
{
    private const string S3_BUCKET = "us-east-1-886110331954-us-east-2-an";
    private const string CDN_BASE = "https://cdn.lagdaemon.com";
    
    public static string GetAssetUrl(string path)
    {
        // Returns: https://cdn.lagdaemon.com/assets/learn/images/staff.png
        return $"{CDN_BASE}/{path}";
    }
}
```

**Usage in Blazor:**
```html
<img src="@AssetHelper.GetAssetUrl("assets/learn/images/staff.png")" />
```

## Phase 2: File Organization Plan

### Assets to Migrate (Priority Order)

#### High Priority (Critical space savings)
- **Old logs** (`/var/log/`) — ~100-200 MB
  - Archive to `archives/old-logs/`
  - Keep recent 30 days on server
- **Cache files** (`/var/cache/apt/`, `/var/lib/snapd/cache/`)
  - `apt cache`: ~142 MB (can be rebuilt)
  - `snapd cache`: ~181 MB (can be rebuilt)
  - Clear locally, S3 backup optional

#### Medium Priority (Asset organization)
- **Graphics & UI assets** (`/var/www/lagdaemon/assets/`)
  - Categorize by app: learn/, dashboard/, common/
  - Estimated: ~50-100 MB
- **Music notation PDFs** (if any)
  - `assets/learn/pdfs/` or similar
- **User-uploaded content** (if any)
  - `user-content/` with date subdirs

#### Lower Priority (One-time archives)
- **Old database backups** (if stored on server)
  - `backups/db/` with date folders
- **Legacy application versions**
  - `archives/app-versions/`

### Naming Convention

```
s3://bucket/category/type/year/month/filename

Examples:
- assets/learn/images/2024/03/staff.png
- assets/learn/icons/2024/09/treble-clef.svg
- archives/logs/2024/06/application.log
- backups/db/2024/07/kwestkarz-2024-07-15.sql
- user-content/lessons/2024/09/001/submission.pdf
```

## Phase 3: Migration Process

### Inventory Step
```bash
# On server, list what we have
du -sh /var/www/lagdaemon/assets/*
du -sh /var/log/*
du -sh /var/cache/apt
du -sh /var/lib/snapd/cache
```

### Migration Batches

**Batch 1: Logs** (100-200 MB freed)
1. Archive recent logs to S3
2. Delete old logs on server
3. Verify log rotation still works

**Batch 2: Cache** (323 MB freed)
1. Clear apt cache (rebuilt on next update)
2. Clear snapd cache (rebuilt on next snap use)
3. Verify space

**Batch 3: Graphics Assets** (50-100 MB freed)
1. Organize assets locally first
2. Upload to S3 with proper structure
3. Update all image references in code
4. Test all pages load correctly
5. Delete from server

**Batch 4: Other static content**
1. PDFs, old backups, etc.
2. Same verify/test process

### Verification Checklist

For each migrated file type:
- [ ] Files exist in S3 at expected path
- [ ] All URLs in code updated (if applicable)
- [ ] CloudFront cache cleared
- [ ] All pages render correctly
- [ ] No 404 errors in logs
- [ ] Old files deleted from server (after verification)
- [ ] Disk space freed on server

## Phase 4: Disk Space Goal

| Item | Size | Action | Result |
|------|------|--------|--------|
| Current usage | 6.3 GB (96%) | Baseline | - |
| Model (already done) | -256 MB | Migrated to S3 | 6.0 GB (90%) |
| Logs | -150 MB | Archive + delete | 5.85 GB (87%) |
| apt/snapd cache | -323 MB | Clear locally | 5.5 GB (82%) |
| Asset graphics | -75 MB | Migrate to S3 | 5.4 GB (81%) |
| **Target** | **5.4 GB** | **All done** | **81% capacity** |

**Goal:** Bring server below 85% capacity, giving breathing room for growth.

## Implementation Notes

- **No data loss** — Everything backed up to S3 before deletion
- **Gradual migration** — Do one batch at a time, verify completely before next
- **Reversible** — If needed, can restore from S3 backups
- **Future-proof** — New assets go directly to S3, not server

## Next Steps

1. Implement AssetHelper class in codebase
2. Audit /var/www/lagdaemon for assets to migrate
3. Create S3 folder structure
4. Start Batch 1 (logs)
5. Monitor disk space and application health after each batch
