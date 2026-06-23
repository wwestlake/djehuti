#!/bin/bash
# Run once on the EC2 server as ubuntu.
# ssh -i your-key.pem ubuntu@ec2-18-227-80-8.us-east-2.compute.amazonaws.com
# then: bash server-bootstrap.sh

set -e

# Create directories
sudo mkdir -p /var/www/djehuti
sudo mkdir -p /opt/djehuti-api
sudo chown ubuntu:ubuntu /var/www/djehuti /opt/djehuti-api

# Install systemd service
sudo cp /tmp/djehuti-api.service /etc/systemd/system/djehuti-api.service
sudo systemctl daemon-reload

# ── Nginx setup ──────────────────────────────────────────────────────────────
# Find the existing default site config and add the location blocks.
# Edit the file below to match your actual nginx site config path.
NGINX_SITE=/etc/nginx/sites-available/default

echo ""
echo "Add the contents of nginx-djehuti.conf into ${NGINX_SITE} inside the server {} block."
echo "Then run: sudo nginx -t && sudo systemctl reload nginx"
echo ""
echo "Also add your ANTHROPIC_API_KEY to /etc/systemd/system/djehuti-api.service"
echo "Then run: sudo systemctl daemon-reload && sudo systemctl enable djehuti-api"
