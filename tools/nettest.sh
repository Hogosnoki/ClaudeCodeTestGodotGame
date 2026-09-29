#!/usr/bin/env bash
# Online co-op check: runs two copies of the game without a window on this machine, one hosting
# and one joining it (127.0.0.1), and has them test what travels between them (see
# godot/DaggerCave/Main.NetTest.cs). Prints both games' checks; exits 0 only if both pass.
#
#   tools/nettest.sh [log dir]
#   JOIN_HERO=elementalist tools/nettest.sh [log dir]   (the joining player's hero; swordsman by default)
set -u
cd "$(dirname "$0")/../godot"
logs="${1:-$(mktemp -d)}"
mkdir -p "$logs"
godot="${GODOT:-godot}"

"$godot" --headless --path . -- --nettest=host --hero=warden > "$logs/host.log" 2>&1 &
host=$!
sleep 3
"$godot" --headless --path . -- --nettest=join --hero="${JOIN_HERO:-swordsman}" > "$logs/join.log" 2>&1 &
join=$!

wait "$join"; join_code=$?
wait "$host"; host_code=$?

echo "---- host ($host_code)"; grep "\[nettest\]" "$logs/host.log"
echo "---- join ($join_code)"; grep "\[nettest\]" "$logs/join.log"
echo "(full logs in $logs)"
[ "$host_code" -eq 0 ] && [ "$join_code" -eq 0 ]
