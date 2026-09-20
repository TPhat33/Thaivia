"""Allow `python -m map_pipeline ...` as an alternative to the `thaivia`
console script."""

from map_pipeline.cli import main

if __name__ == "__main__":
    raise SystemExit(main())
