"""Record dungeon exit times and success totals using the shared QA framework."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from qa_framework import QAReportReceiver, UnitReport, evaluate_unit


class DungeonRunRecorder:
    """Store each received attempt once and resume success statistics from the same JSONL file."""

    def __init__(self, output: Path):
        self.output = output
        self.attempts: set[str] = set()
        self.total_runs = 0
        self.successes = 0
        self.total_exit_seconds = 0.0
        if output.exists():
            with output.open(encoding="utf-8") as stream:
                for line in stream:
                    self._add(UnitReport.from_dict(json.loads(line)["report"]))

    def _add(self, report: UnitReport):
        if report.attempt_id in self.attempts:
            return None
        result = evaluate_unit(report, {
            "reached_exit": lambda unit: unit.evidence["reached_exit"],
        })
        if result.passed:
            self.total_exit_seconds += report.evidence["elapsed_seconds"]
            self.successes += 1
        self.total_runs += 1
        self.attempts.add(report.attempt_id)
        return result

    def record(self, report: UnitReport) -> dict | None:
        result = self._add(report)
        if result is None:
            return None

        time_to_exit = None
        mean_exit_seconds = None
        if result.passed:
            time_to_exit = report.evidence["elapsed_seconds"]
        if self.successes:
            mean_exit_seconds = self.total_exit_seconds / self.successes
        record = result.to_dict()
        record["time_to_exit_seconds"] = time_to_exit
        record["summary"] = {
            "successes": self.successes,
            "failures": self.total_runs - self.successes,
            "total_runs": self.total_runs,
            "success_rate": self.successes / self.total_runs,
            "mean_exit_seconds": mean_exit_seconds,
        }
        self.output.parent.mkdir(parents=True, exist_ok=True)
        with self.output.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(record, allow_nan=False) + "\n")
        return record


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8800)
    parser.add_argument(
        "--output", type=Path,
        default=Path(__file__).resolve().parents[1] / "Logs/qa-results/dungeon-runs.jsonl",
    )
    args = parser.parse_args()
    recorder = DungeonRunRecorder(args.output)
    with QAReportReceiver(port=args.port) as receiver:
        print(f"Recording dungeon runs on 127.0.0.1:{receiver.port} to {args.output}", flush=True)
        try:
            while True:
                record = recorder.record(receiver.receive())
                if record is not None:
                    print(json.dumps(record, allow_nan=False), flush=True)
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    main()
