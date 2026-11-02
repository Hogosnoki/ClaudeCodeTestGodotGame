#!/usr/bin/env bash
# Getting back into a run (godot/DaggerCave/Main.Rejoin.cs): three copies of the game without a window. One hosts a run; one
# joins, has its connection drop (it must reconnect by itself, as the same hero), then dies outright; a fresh copy joins with
# the same address and must come back as that hero, from what was written down. See Main.RejoinTest.cs.
#
#   tools/rejointest.sh [log dir]
set -u
cd "$(dirname "$0")/../godot"
logs="${1:-$(mktemp -d)}"
mkdir -p "$logs"
godot="${GODOT:-godot}"
args="--headless --path ."

"$godot" $args -- --nettest=rjhost --hero=warden > "$logs/host.log" 2>&1 &
host=$!
sleep 3
"$godot" $args -- --nettest=rjjoin --hero="${JOIN_HERO:-swordsman}" > "$logs/join.log" 2>&1
"$godot" $args -- --nettest=rjback --hero=vitalist > "$logs/back.log" 2>&1 &
back=$!

wait "$back"; back_code=$?
wait "$host"; host_code=$?

echo "---- host ($host_code)"; grep "\[nettest\]" "$logs/host.log"
echo "---- join"; grep "\[nettest\]" "$logs/join.log"
echo "---- back ($back_code)"; grep "\[nettest\]" "$logs/back.log"
echo "(full logs in $logs)"
grep -q "rjjoin PASS" "$logs/join.log" && [ "$host_code" -eq 0 ] && [ "$back_code" -eq 0 ]
