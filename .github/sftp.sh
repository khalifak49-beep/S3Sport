#!/usr/bin/env bash
# .github/sftp.sh "<lftp commands>": runs lftp commands on the MonsterASP site over SFTP.
# Uses FTP_SERVER / FTP_USER / FTP_PASSWORD from the environment and the pinned host keys in .github/known_hosts,
# so a server presenting another key is refused. Stops at the first failing command with a non-zero exit code.
set -euo pipefail
export LFTP_PASSWORD="$FTP_PASSWORD"
kh="$(cd "$(dirname "$0")" && pwd)/known_hosts"
lftp --env-password -u "$FTP_USER" -e "set cmd:fail-exit yes; set sftp:auto-confirm no; \
set sftp:connect-program 'ssh -a -x -o UserKnownHostsFile=$kh -o StrictHostKeyChecking=yes'; \
set net:max-retries 3; set net:reconnect-interval-base 5; set net:timeout 30; $1; bye" "sftp://$FTP_SERVER"
