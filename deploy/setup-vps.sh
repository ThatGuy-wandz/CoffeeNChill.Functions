#!/usr/bin/env bash
# One-time VPS setup for CoffeeNChill (Ubuntu 22.04/24.04, x86_64).
# Run with:  sudo bash setup-vps.sh
set -euo pipefail

APP_USER="${SUDO_USER:-root}"
APP_HOME=$(getent passwd "$APP_USER" | cut -d: -f6)
APP_DIR="$APP_HOME/coffeenchill"

echo ">>> Installing Docker Engine + Compose plugin"
apt-get update -y
apt-get install -y ca-certificates curl gnupg ufw
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" > /etc/apt/sources.list.d/docker.list
apt-get update -y
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
systemctl enable --now docker
usermod -aG docker "$APP_USER"

echo ">>> Adding 2GB swap (the Functions container is memory hungry)"
if ! swapon --show | grep -q swapfile; then
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo ">>> Firewall: allow SSH and the gateway port only"
ufw allow OpenSSH
ufw allow 5000/tcp
ufw --force enable

echo ">>> Creating $APP_DIR and a starter .env"
mkdir -p "$APP_DIR"
if [ ! -f "$APP_DIR/.env" ]; then
  cat > "$APP_DIR/.env" <<ENV
DOCKERHUB_USERNAME=CHANGE_ME
IMAGE_TAG=latest
JWT_KEY=$(openssl rand -base64 48)
ADMIN_USERNAME=admin
ADMIN_EMAIL=admin@coffeenchill.ac.za
ADMIN_PASSWORD=CHANGE_ME_strong_password
STAFF_USERNAME=barista
STAFF_EMAIL=barista@coffeenchill.ac.za
STAFF_PASSWORD=CHANGE_ME_strong_password
ENV
  chmod 600 "$APP_DIR/.env"
fi
chown -R "$APP_USER":"$APP_USER" "$APP_DIR"

echo ">>> Done. Edit $APP_DIR/.env (nano $APP_DIR/.env), then log out and back in."