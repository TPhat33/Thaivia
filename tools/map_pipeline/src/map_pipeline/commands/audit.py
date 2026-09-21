"""`thaivia audit` -- render docs/data/pilot-audit.md from a built
MapPack's quality report.

When no `--pack` is given, this command leaves the honest
blocked/not_measured placeholder in `docs/data/pilot-audit.md` exactly
as it is -- it never zero-fills fake numbers and never claims a real
import happened when one has not.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from map_pipeline import exit_codes
from map_pipeline.config import ConfigError, load_pilot_area
from map_pipeline.paths import repo_root


def add_subparser(subparsers: argparse._SubParsersAction) -> None:
    p = subparsers.add_parser("audit", help="Generate the pilot-area data audit report from a built MapPack")
    p.add_argument(
        "--config",
        default="configs/pilot-area.json",
        help="Path to pilot-area config (default: configs/pilot-area.json)",
    )
    p.add_argument("--pack", default=None, help="Path to a baked MapPack JSON file to audit")
    p.add_argument("--out", default="docs/data/pilot-audit.md", help="Output path for the audit markdown")
    p.set_defaults(func=run)


def run(args) -> int:
    try:
        load_pilot_area()
    except ConfigError as exc:
        print(f"thaivia audit: config error: {exc}")
        return exit_codes.GENERAL_ERROR

    if not args.pack:
        print("thaivia audit: no --pack given; leaving docs/data/pilot-audit.md as the honest")
        print("  blocked/not_measured placeholder unchanged. Run `thaivia build` first to produce")
        print("  a real MapPack, then re-run `thaivia audit --pack <path>`.")
        return exit_codes.NOT_IMPLEMENTED

    pack_path = Path(args.pack)
    if not pack_path.is_file():
        print(f"thaivia audit: MapPack not found: {pack_path}")
        return exit_codes.GENERAL_ERROR
    pack = json.loads(pack_path.read_text(encoding="utf-8"))

    out_path = Path(args.out)
    if not out_path.is_absolute():
        out_path = repo_root() / out_path
    markdown = render_audit_markdown(pack)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(markdown, encoding="utf-8")
    print(f"thaivia audit: wrote {out_path}")
    return exit_codes.OK


def render_audit_markdown(pack: dict) -> str:
    payload = pack["payload"]
    manifest = payload["manifest"]
    prov = payload["provenance"]
    qr = payload["quality_report"]
    synthetic = bool(prov.get("synthetic"))

    lines: list[str] = []
    title_suffix = " (SYNTHETIC -- NOT the real pilot area)" if synthetic else ""
    lines.append(f"# Pilot area data audit -- {manifest['map_id']}{title_suffix}")
    lines.append("")
    if synthetic:
        lines.append(
            "**status: synthetic-derived -- NOT real OSM data, does not represent any real "
            "place in Thailand, must never be mistaken for the real pilot.**"
        )
        if prov.get("synthetic_notice"):
            lines.append("")
            lines.append(f"> {prov['synthetic_notice']}")
    else:
        lines.append("**status: built from a real acquired OSM source**")
    lines.append("")
    lines.append(f"MapPack `content_hash`: `{pack['content_hash']}`")
    lines.append("")
    lines.append(f"Baked at: `{pack['volatile']['baked_at']}`")
    lines.append("")
    lines.append("## Source snapshot")
    lines.append("")
    lines.append(f"- Source kind: `{prov['source_kind']}`")
    lines.append(f"- Source location: `{prov['source_location']}`")
    lines.append(f"- Retrieval time: `{pack['volatile']['source_retrieved_at']}`")
    lines.append(f"- Source snapshot time: `{prov['source_snapshot_timestamp']}`")
    lines.append(f"- Source snapshot unknown reason: `{prov['source_snapshot_unknown_reason']}`")
    lines.append(f"- SHA-256: `{prov['sha256']}`")
    lines.append(f"- Size: {prov['size_bytes']} bytes")
    lines.append(f"- Importer version: `{manifest['importer_version']}`")
    lines.append(f"- Schema version: `{manifest['schema_version']}`")
    lines.append(f"- Settings hash: `{manifest['settings_hash']}`")
    lines.append(f"- CRS: `{manifest['crs_code']}`")
    if manifest["crs_validation_warnings"]:
        lines.append("- CRS validation warnings:")
        for w in manifest["crs_validation_warnings"]:
            lines.append(f"  - {w}")
    lines.append("")

    lines.append("## Feature inventory (not a \"coverage %\" -- there is no ground truth)")
    lines.append("")
    lines.append("| Type | Count |")
    lines.append("|---|---|")
    for k, v in sorted(qr["retained_counts"].items()):
        lines.append(f"| {k} | {v} |")
    lines.append("")

    lines.append("## Known/unknown attribute counts")
    lines.append("")
    lines.append("| Attribute | Unknown count |")
    lines.append("|---|---|")
    for k, v in sorted(qr["unknown_counts"].items()):
        lines.append(f"| {k} | {v} |")
    lines.append("")

    rt = qr["roundtrip_error_m"]
    lines.append("## Round-trip geometry error (measured, not asserted)")
    lines.append("")
    lines.append(
        f"max={rt['max']:.6f} m, mean={rt['mean']:.6f} m, sample_count={rt['sample_count']} "
        "(budget: <= 0.10 m)"
    )
    lines.append("")

    lines.append("## Topology conflicts / dropped / quarantined / incomplete relations / unsupported restrictions")
    lines.append("")
    for section_name, items in (
        ("Topology conflicts", qr["topology_conflicts"]),
        ("Dropped features", qr["dropped"]),
        ("Quarantined features", qr["quarantined"]),
        ("Incomplete relations", qr["incomplete_relations"]),
        ("Unsupported turn restrictions", qr["unsupported_restrictions"]),
        ("Duplicate POI/building associations", qr["duplicate_associations"]),
    ):
        lines.append(f"### {section_name} ({len(items)})")
        lines.append("")
        if not items:
            lines.append("(none)")
        else:
            for item in items:
                lines.append(f"- `{item['feature_id']}`: {item['detail']}")
        lines.append("")

    lines.append("## Retained vs rejected")
    lines.append("")
    lines.append(f"- Retained: `{qr['retained_counts']}`")
    lines.append(f"- Rejected: `{qr['rejected_counts']}`")
    lines.append("")

    attribution = prov["attribution"]
    lines.append("## Attribution")
    lines.append("")
    lines.append(
        f"{attribution['notice']} -- [ODbL]({attribution['odbl_license_url']}) -- "
        f"[copyright/attribution]({attribution['copyright_url']})"
    )
    lines.append("")

    lines.append("## Verdict")
    lines.append("")
    if synthetic:
        lines.append(
            "This audit was generated from a synthetic fixture for pipeline testing. It does "
            "NOT establish that the real th-bkk-pilot-001 crop is usable; that still requires "
            "a real acquired source (see docs/decisions/0003)."
        )
    else:
        lines.append(
            "Generated by `thaivia audit` from a real acquired MapPack. See the sections above "
            "for retained/rejected counts and open issues before treating this crop as usable."
        )
    lines.append("")
    return "\n".join(lines)
