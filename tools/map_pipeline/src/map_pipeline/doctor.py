"""`thaivia doctor` — a real environment/toolchain health check.

This is the one G0 subcommand that is fully implemented: it inspects the
Python interpreter, verifies required dependencies actually import, loads
and schema-validates the shipped configs, checks whether a local OSM
source cache exists, and probes configured source endpoints for
reachability. It never claims success it has not observed.
"""

from __future__ import annotations

import importlib
import importlib.metadata
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from enum import Enum

from map_pipeline import exit_codes
from map_pipeline.config import ConfigError, load_pilot_area, load_sources
from map_pipeline.paths import cache_dir

MIN_PYTHON = (3, 11)
REQUIRED_IMPORTS = ["osmium", "pyproj", "shapely", "jsonschema"]
REACHABILITY_TIMEOUT_S = 5.0


class Status(str, Enum):
    AVAILABLE = "available"
    MISSING = "missing"
    BLOCKED = "blocked"
    UNVERIFIED = "unverified"


@dataclass
class Check:
    name: str
    status: Status
    detail: str
    hard_failure: bool = False


@dataclass
class DoctorReport:
    checks: list[Check] = field(default_factory=list)

    @property
    def has_hard_failure(self) -> bool:
        return any(c.hard_failure for c in self.checks)


def check_python_version() -> Check:
    actual = sys.version_info[:3]
    if actual[:2] < MIN_PYTHON:
        return Check(
            "python_version",
            Status.MISSING,
            f"Python {'.'.join(map(str, actual))} found; >= "
            f"{'.'.join(map(str, MIN_PYTHON))} required.",
            hard_failure=True,
        )
    note = ""
    if actual[:2] != (3, 12):
        note = " (plan's proposed baseline was 3.12; see ADR-0001)"
    return Check(
        "python_version",
        Status.AVAILABLE,
        f"Python {'.'.join(map(str, actual))} at {sys.executable}{note}",
    )


def check_dependency(module_name: str) -> Check:
    try:
        mod = importlib.import_module(module_name)
    except Exception as exc:  # noqa: BLE001 - report any import failure honestly
        return Check(
            f"dependency:{module_name}",
            Status.MISSING,
            f"import {module_name} failed: {exc}",
            hard_failure=True,
        )
    # The PyPI distribution name matches the import name for every
    # required dependency ("osmium" was renamed from "pyosmium" upstream;
    # see docs/evidence/g0-pip-resolve-pyosmium.log).
    dist_name = module_name
    try:
        version = importlib.metadata.version(dist_name)
    except importlib.metadata.PackageNotFoundError:
        version = getattr(mod, "__version__", "unknown")
    return Check(
        f"dependency:{module_name}",
        Status.AVAILABLE,
        f"{dist_name} {version} imports OK",
    )


def check_config(loader, name: str) -> Check:
    try:
        loader()
    except ConfigError as exc:
        return Check(f"config:{name}", Status.MISSING, str(exc), hard_failure=True)
    return Check(f"config:{name}", Status.AVAILABLE, f"{name} present and schema-valid")


def check_cache() -> Check:
    d = cache_dir()
    if not d.is_dir():
        return Check(
            "source_cache",
            Status.MISSING,
            f"{d} does not exist yet; no OSM source has been acquired (expected in G0)",
        )
    entries = [p for p in d.iterdir() if p.is_file()]
    if not entries:
        return Check(
            "source_cache",
            Status.MISSING,
            f"{d} exists but is empty; no OSM source has been acquired (expected in G0)",
        )
    names = ", ".join(sorted(p.name for p in entries)[:10])
    return Check("source_cache", Status.AVAILABLE, f"{d} contains: {names}")


def _probe_url(url: str) -> tuple[Status, str]:
    req = urllib.request.Request(url, method="HEAD")
    try:
        with urllib.request.urlopen(req, timeout=REACHABILITY_TIMEOUT_S) as resp:  # noqa: S310
            return Status.AVAILABLE, f"HTTP {resp.status}"
    except urllib.error.HTTPError as exc:
        if exc.code == 403:
            return Status.BLOCKED, "HTTP 403 (policy denial, see docs/environment.md)"
        return Status.AVAILABLE, f"HTTP {exc.code} (host answered)"
    except (urllib.error.URLError, TimeoutError, OSError) as exc:
        detail = str(exc)
        if "403" in detail and "Forbidden" in detail:
            # Proxy CONNECT tunnels report policy denials as a URLError
            # (not an HTTPError) because the TLS tunnel itself is refused.
            return Status.BLOCKED, f"tunnel/connect denied: {detail} (see docs/environment.md)"
        return Status.UNVERIFIED, f"connection failed: {detail}"


def _cached_reachability_checks(cfg) -> list[Check]:
    """Reports reachability from the status already recorded in
    configs/sources.json (last confirmed during the G0 session), instead
    of re-probing hosts the egress proxy has already denied by policy.
    This is the default: `thaivia doctor` should not re-attempt hosts
    that are blocked as a matter of organization policy on every single
    invocation."""
    checks: list[Check] = []
    checked_at = cfg.data.get("checked_at", "unknown date")
    for src in cfg.data.get("sources", []):
        name = src.get("name", "?")
        recorded = src.get("recorded_status", "unverified")
        detail = src.get("recorded_status_detail", "no recorded_status_detail in configs/sources.json")
        status = {
            "available": Status.AVAILABLE,
            "blocked": Status.BLOCKED,
            "unverified": Status.UNVERIFIED,
        }.get(recorded, Status.UNVERIFIED)
        checks.append(
            Check(
                f"source:{name}",
                status,
                f"[cached result from {checked_at}, not re-probed] {detail}",
            )
        )
    return checks


def _live_reachability_checks(cfg) -> list[Check]:
    checks: list[Check] = []
    for src in cfg.data.get("sources", []):
        name = src.get("name", "?")
        url = src.get("probe_url") or src.get("base_url")
        if not url:
            checks.append(
                Check(f"source:{name}", Status.UNVERIFIED, "no probe_url/base_url configured")
            )
            continue
        status, detail = _probe_url(url)
        checks.append(Check(f"source:{name}", status, f"[live probe] {url} -> {detail}"))
    return checks


def check_sources_reachability(probe_network: bool = False) -> list[Check]:
    try:
        cfg = load_sources()
    except ConfigError as exc:
        return [Check("source_reachability", Status.MISSING, str(exc))]
    if probe_network:
        return _live_reachability_checks(cfg)
    return _cached_reachability_checks(cfg)


def run_doctor(probe_network: bool = False) -> DoctorReport:
    report = DoctorReport()
    report.checks.append(check_python_version())
    for mod in REQUIRED_IMPORTS:
        report.checks.append(check_dependency(mod))
    report.checks.append(check_config(load_pilot_area, "pilot-area.json"))
    report.checks.append(check_config(load_sources, "sources.json"))
    report.checks.append(check_cache())
    report.checks.extend(check_sources_reachability(probe_network=probe_network))
    return report


def format_report(report: DoctorReport) -> str:
    lines = ["thaivia doctor -- environment report", ""]
    width = max((len(c.name) for c in report.checks), default=10)
    for c in report.checks:
        tag = c.status.value.upper()
        lines.append(f"[{tag:<10}] {c.name.ljust(width)} : {c.detail}")
    lines.append("")
    if report.has_hard_failure:
        lines.append("RESULT: hard problems found (see MISSING entries marked hard failure)")
    else:
        lines.append("RESULT: no hard problems found")
    return "\n".join(lines)


def main(args) -> int:
    probe_network = bool(getattr(args, "probe_network", False))
    report = run_doctor(probe_network=probe_network)
    print(format_report(report))
    if not probe_network:
        print(
            "\nNote: source reachability above is a CACHED result from configs/sources.json, "
            "not a live probe. Pass --probe-network to re-probe live (this will re-attempt "
            "hosts already denied by organization egress policy; see ADR-0003)."
        )
    return exit_codes.GENERAL_ERROR if report.has_hard_failure else exit_codes.OK
