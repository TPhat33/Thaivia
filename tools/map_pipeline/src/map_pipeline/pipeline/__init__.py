"""G1 real-map data pipeline: acquire -> source lock/hash -> buffered
extract -> entity parse -> project -> normalize -> graph -> audit ->
MapPack bake -> verify.

Every module in this package is pure logic (no filesystem/network I/O)
except `sourcelock.py`'s file read/write helpers, which are kept thin and
separate from the hashing/validation logic so the logic stays testable
without touching a disk. CLI commands under `map_pipeline.commands` are
the only place that performs I/O side effects end to end.
"""
