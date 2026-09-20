"""Loading and JSON-Schema validation for the pipeline's config files."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import jsonschema

from map_pipeline.paths import configs_dir, schemas_dir


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
