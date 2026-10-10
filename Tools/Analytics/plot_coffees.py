"""Turn a real Firebase JSON export into a CSV and a coffee-use bar chart.

Usage: python3 plot_coffees.py events.json --build alpha-2026-10-19 --out results
The export may be the whole database or only its events subtree.
"""
import argparse
import csv
import json
from collections import Counter
from pathlib import Path

ZONES = ["Courtyard", "Corridor", "Fork", "Hideout"]


def flatten(node):
    if not isinstance(node, dict):
        return
    if "eventId" in node and "eventName" in node:
        yield node
    else:
        for value in node.values():
            yield from flatten(value)


def summarize(export, build):
    rows_by_id = {
        row["eventId"]: row for row in flatten(export) if row.get("buildId") == build
    }
    rows = list(rows_by_id.values())
    sessions = {r["sessionId"] for r in rows if r["eventName"] == "session_start"}
    cups = [r for r in rows if r["eventName"] == "coffee_drunk" and r["sessionId"] in sessions]
    by_zone = Counter(r["zone"] for r in cups)
    by_session = Counter(r["sessionId"] for r in cups)
    frequency = Counter(by_session.get(session, 0) for session in sessions)
    first_hideout = {}
    for row in rows:
        if row["sessionId"] in sessions and row["eventName"] == "zone_enter" and row["zone"] == "Hideout":
            session = row["sessionId"]
            first_hideout[session] = min(first_hideout.get(session, 100), row["sequence"])
    before_hideout = Counter(
        r["sessionId"] for r in cups
        if r["sessionId"] in first_hideout and r["sequence"] < first_hideout[r["sessionId"]]
    )
    all_three = sum(before_hideout[session] >= 3 for session in first_hideout)
    return {
        "sessions": len(sessions), "rows": len(rows), "by_zone": by_zone,
        "frequency": frequency, "reached_hideout": len(first_hideout),
        "all_three_before_hideout": all_three,
        "hypothesis_fraction": all_three / len(first_hideout) if first_hideout else None,
        "orphan_coffee_events": sum(r["eventName"] == "coffee_drunk" and r["sessionId"] not in sessions for r in rows)
    }


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("input", type=Path)
    parser.add_argument("--build", required=True)
    parser.add_argument("--out", type=Path, default=Path("results"))
    args = parser.parse_args()
    summary = summarize(json.loads(args.input.read_text()), args.build)
    if summary["sessions"] == 0:
        raise SystemExit("No session_start records for this build; do not invent a denominator.")
    args.out.mkdir(parents=True, exist_ok=True)
    with (args.out / "coffees_by_zone.csv").open("w", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["zone", "coffee_events", "started_sessions", "mean_cups_per_started_session"])
        for zone in ZONES:
            count = summary["by_zone"].get(zone, 0)
            writer.writerow([zone, count, summary["sessions"], count / summary["sessions"]])
    with (args.out / "coffee_count_distribution.csv").open("w", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["cups_in_attempt", "sessions"])
        for count in range(4):
            writer.writerow([count, summary["frequency"].get(count, 0)])
    report = {key: value for key, value in summary.items() if key not in ("by_zone", "frequency")}
    (args.out / "summary.json").write_text(json.dumps(report, indent=2))
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    fig, axis = plt.subplots(figsize=(7, 4))
    values = [summary["by_zone"].get(zone, 0) / summary["sessions"] for zone in ZONES]
    axis.bar(ZONES, values, color="#4A647A")
    axis.set_ylim(0, max(1, max(values) * 1.2))
    axis.set_ylabel("Mean cups per started attempt")
    axis.set_title(f"Coffee use by zone | {args.build} | n={summary['sessions']}")
    fig.tight_layout()
    chart_path = args.out / "coffees_by_zone.png"
    fig.savefig(chart_path, dpi=180)
    plt.close(fig)
    with chart_path.open("rb") as handle:
        if handle.read(8) != b"\x89PNG\r\n\x1a\n":
            raise SystemExit("Chart export failed: expected a non-empty PNG file.")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
