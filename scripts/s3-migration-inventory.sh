#!/bin/bash

# S3 Migration Inventory Script
# Run on the kwestkarz server to catalog all files that should be migrated to S3
# Usage: ssh -i KwestKarz.pem ubuntu@kwestkarz.com < s3-migration-inventory.sh

echo "=========================================="
echo "S3 MIGRATION INVENTORY"
echo "=========================================="
echo "Generated: $(date)"
echo ""

# Get disk usage summary
echo "DISK USAGE SUMMARY:"
df -h / | tail -1
echo ""

# Inventory: Application assets
echo "========== APPLICATION ASSETS =========="
if [ -d "/var/www/lagdaemon/assets" ]; then
    echo "Learn app assets:"
    du -sh /var/www/lagdaemon/assets/* 2>/dev/null | sort -rh
    echo ""
fi

if [ -d "/var/www/kwestkarz/assets" ]; then
    echo "Dashboard assets:"
    du -sh /var/www/kwestkarz/assets/* 2>/dev/null | sort -rh
    echo ""
fi

# Inventory: Logs
echo "========== LOGS =========="
echo "Total log size:"
du -sh /var/log/ 2>/dev/null
echo ""
echo "Largest log files:"
find /var/log -type f -exec du -h {} + 2>/dev/null | sort -rh | head -10
echo ""

# Inventory: Cache
echo "========== CACHE =========="
echo "apt cache:"
du -sh /var/cache/apt 2>/dev/null
echo ""
echo "snapd cache:"
du -sh /var/lib/snapd/cache 2>/dev/null || echo "0 (or not present)"
echo ""
echo "Other cache dirs:"
du -sh /var/cache/* 2>/dev/null | sort -rh | head -5
echo ""

# Inventory: Backups (if stored on server)
echo "========== BACKUPS =========="
if [ -d "/var/backups" ]; then
    echo "Database/system backups:"
    du -sh /var/backups/* 2>/dev/null | sort -rh
    echo ""
fi

# Inventory: Web root space breakdown
echo "========== WEB ROOT BREAKDOWN =========="
echo "Lagdaemon (/var/www/lagdaemon):"
du -sh /var/www/lagdaemon/* 2>/dev/null | sort -rh
echo ""
echo "KwestKarz (/var/www/kwestkarz):"
du -sh /var/www/kwestkarz/* 2>/dev/null | sort -rh
echo ""

# Summary
echo "=========================================="
echo "END OF INVENTORY"
echo "=========================================="
