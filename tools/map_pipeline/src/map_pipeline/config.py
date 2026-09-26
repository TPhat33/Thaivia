"""Loading and JSON-Schema validation for the pipeline's config files."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import jsonschema

from map_pipeline.paths import configs_dir, repo_root, schemas_dir


class ConfigError(Exception):
    """Raised when a config file is missing, unreadable, or invalid."""


@dataclass
class ValidatedConfig:
    path: Path
    schema_path: Path
    data: dict[str, Any]


def _load_json(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise ConfigError(f"config file not found: {path}")
    try:
        with path.open("r", encoding="utf-8") as fh:
            return json.load(fh)
    except json.JSONDecodeError as exc:
        raise ConfigError(f"{path} is not valid JSON: {exc}") from exc


def validate_against_schema(data: dict[str, Any], schema_path: Path) -> None:
    if not schema_path.is_file():
        raise ConfigError(f"schema file not found: {schema_path}")
    with schema_path.open("r", encoding="utf-8") as fh:
        schema = json.load(fh)
    validator_cls = jsonschema.validators.validator_for(schema)
    validator_cls.check_schema(schema)
    validator = validator_cls(schema)
    errors = sorted(validator.iter_errors(data), key=lambda e: list(e.path))
    if errors:
        messages = "; ".join(
            f"{'/'.join(str(p) for p in e.path) or '<root>'}: {e.message}" for e in errors
        )
        raise ConfigError(f"{schema_path.name}: schema validation failed: {messages}")


def load_and_validate(config_filename: str, schema_filename: str) -> ValidatedConfig:
    path = configs_dir() / config_filename
    schema_path = schemas_dir() / schema_filename
    data = _load_json(path)
    validate_against_schema(data, schema_path)
    return ValidatedConfig(path=path, schema_path=schema_path, data=data)


def load_pilot_area() -> ValidatedConfig:
    return load_and_validate("pilot-area.json", "pilot-area.schema.json")


def load_sources() -> ValidatedConfig:
    return load_and_validate("sources.json", "sources.schema.json")


# --- Area registry (G6-01) -------------------------------------------------
#
# Generalizes the pipeline from one hardcoded pilot area to N declared
# areas (plan section 4/5). `configs/areas/index.json` lists every
# registered area id and where its own config file lives; each area's
# config file is validated against the same pilot-area.schema.json used
# for the original pilot (already generic: map_id/bbox/CRS/driving_side/
# acquire_budget -- nothing pilot-specific in the shape itself).
#
# Back-compat contract: every CLI command keeps working exactly as
# before when `--area` is not passed -- it loads `--config` (default
# `configs/pilot-area.json`) directly, with **no** dependency on
# `configs/areas/index.json` existing at all. Only an explicit
# `--area <id>` consults the registry. This is deliberate: the existing
# G1-G5 tests build a minimal fake repo root that carries only
# `configs/pilot-area.json` (no `configs/areas/` at all), and that must
# keep passing unchanged.

DEFAULT_AREA_ID = "th-bkk-pilot-001"


def load_areas_index() -> ValidatedConfig:
    return load_and_validate("areas/index.json", "areas-index.schema.json")


def _area_entries(index_data: dict[str, Any]) -> list[dict[str, Any]]:
    entries = index_data["areas"]
    seen: set[str] = set()
    for entry in entries:
        if entry["id"] in seen:
            raise ConfigError(f"configs/areas/index.json: duplicate area id {entry['id']!r}")
        seen.add(entry["id"])
    return entries


def registered_area_ids() -> list[str]:
    """The list of area ids currently registered in configs/areas/index.json."""
    return [e["id"] for e in _area_entries(load_areas_index().data)]


def resolve_area_config_path(area_id: str) -> Path:
    """Look up a registered area id in the registry and return its
    config file's absolute path. Raises ConfigError for an unknown id,
    listing what *is* registered rather than failing silently."""
    index = load_areas_index().data
    entries = _area_entries(index)
    for entry in entries:
        if entry["id"] == area_id:
            path = Path(entry["config_path"])
            return path if path.is_absolute() else repo_root() / path
    known = ", ".join(e["id"] for e in entries) or "(none registered)"
    raise ConfigError(f"unknown area id {area_id!r}; registered areas: {known}")


def _load_config_at_path(path: Path) -> ValidatedConfig:
    data = _load_json(path)
    schema_path = schemas_dir() / "pilot-area.schema.json"
    validate_against_schema(data, schema_path)
    return ValidatedConfig(path=path, schema_path=schema_path, data=data)


def load_area(area_id: str | None, config_path: str = "configs/pilot-area.json") -> ValidatedConfig:
    """Resolve an area's config for a CLI command.

    - `area_id` is None (the `--area` flag was not passed): load exactly
      the file at `config_path` (default `configs/pilot-area.json`).
      This never touches `configs/areas/index.json` -- the unchanged
      G1-G5 behavior.
    - `area_id` is set: resolve it through the area registry
      (`configs/areas/index.json`) and load *that* area's own config,
      regardless of `config_path`.
    """
    if area_id is None:
        path = Path(config_path)
        if not path.is_absolute():
            path = repo_root() / path
        return _load_config_at_path(path)
    return _load_config_at_path(resolve_area_config_path(area_id))
