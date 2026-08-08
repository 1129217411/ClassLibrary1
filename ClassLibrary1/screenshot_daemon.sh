#!/system/bin/sh
# Screenshot daemon - runs inside Android emulator
# Listens on TCP port, runs screencap per connection, sends raw output
# Usage: screenshot_daemon.sh <port>
PORT=${1:-19000}
while true; do
  /system/bin/screencap | nc -l -p $PORT 2>/dev/null
  usleep 10000 2>/dev/null
done
