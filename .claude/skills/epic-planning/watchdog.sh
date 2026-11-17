#!/usr/bin/env bash
# Orchestrator watchdog — keeps every phase subagent of a wave-based run from stalling unnoticed.
# Emits one stdout line per actionable event:
#   STALL <agent>     — an active agent's transcript has not grown for STALL_SECONDS
#   RESUMED <agent>   — a stalled agent started writing again
#   MISSING <agent>   — an active agent's transcript file disappeared
#   COMMIT <ref> ...  — a new commit landed on the task branch or any worktree branch
#   HEARTBEAT ...     — every 10 minutes, so silence never hides a dead watchdog
# Active agents are listed (one transcript filename per line) in ACTIVE_LIST; the
# orchestrator adds an agent when it launches and removes it when it completes.
# Usage: bash watchdog.sh <subagents transcript dir> <active-list file> <repo path> [stall seconds, default 900]
# The live transcript is <subagents dir>/agent-<agentId>.jsonl (the Agent tool output file stays empty on Windows).

TASKS_DIR="$1"
ACTIVE_LIST="$2"
REPO="$3"
STALL_SECONDS="${4:-900}"
POLL_SECONDS=30
HEARTBEAT_SECONDS=600

declare -A lastSize lastChange alerted

refsSnapshot() {
	git -C "$REPO" for-each-ref --format='%(refname:short) %(objectname:short)' refs/heads 2>/dev/null | sort
}

previousRefs="$(refsSnapshot)"
lastHeartbeat=$(date +%s)

while true; do
	now=$(date +%s)

	if [ -f "$ACTIVE_LIST" ]; then
		while IFS= read -r agent; do
			[ -z "$agent" ] && continue
			file="$TASKS_DIR/$agent"

			if [ ! -f "$file" ]; then
				if [ "${alerted[$agent]}" != "missing" ]; then
					echo "MISSING $agent — transcript file not found"
					alerted[$agent]="missing"
				fi
				continue
			fi

			size=$(stat -c %s "$file" 2>/dev/null || echo 0)

			if [ "${lastSize[$agent]}" != "$size" ]; then
				lastSize[$agent]="$size"
				lastChange[$agent]="$now"

				if [ "${alerted[$agent]}" = "stall" ]; then
					echo "RESUMED $agent — transcript growing again"
				fi

				alerted[$agent]=""
				continue
			fi

			idle=$(( now - ${lastChange[$agent]:-$now} ))

			if [ "$idle" -ge "$STALL_SECONDS" ] && [ "${alerted[$agent]}" != "stall" ]; then
				echo "STALL $agent — no transcript output for $(( idle / 60 )) min"
				alerted[$agent]="stall"
			fi
		done < "$ACTIVE_LIST"
	fi

	currentRefs="$(refsSnapshot)"

	if [ "$currentRefs" != "$previousRefs" ]; then
		comm -13 <(echo "$previousRefs") <(echo "$currentRefs") | while read -r ref sha; do
			subject=$(git -C "$REPO" log -1 --format=%s "$sha" 2>/dev/null)
			echo "COMMIT $ref $sha $subject"
		done
		previousRefs="$currentRefs"
	fi

	if [ $(( now - lastHeartbeat )) -ge "$HEARTBEAT_SECONDS" ]; then
		activeCount=$(grep -c . "$ACTIVE_LIST" 2>/dev/null); activeCount=${activeCount:-0}
		echo "HEARTBEAT watchdog alive — $activeCount active agent(s)"
		lastHeartbeat=$now
	fi

	sleep "$POLL_SECONDS"
done
