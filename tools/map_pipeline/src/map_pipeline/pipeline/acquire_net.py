"""Network acquisition path for `thaivia acquire --from-url`.

Implements the acquire_budget contract from pilot-area.json: a
connect/read timeout, retries with exponential backoff, honoring a
`Retry-After` header, a byte-limit abort mid-stream, and a streaming
SHA-256 (the response body is never buffered whole in memory before
hashing -- it is hashed and written chunk by chunk).

Per ADR-0003 / the standing supervisor instruction: a policy denial
(HTTP 403 on the CONNECT tunnel, which is how this container's egress
proxy reports a blocked host) is treated as TERMINAL on the very first
attempt. This module never retries a policy block and never tries an
alternate host, mirror, port, or protocol to route around one -- it
reports the block honestly and stops.

`urllib`'s `timeout=` covers the whole blocking socket operation (it
does not distinguish "time to connect" from "time to receive the next
chunk" the way some HTTP client libraries do); this module uses
`connect_timeout_s` as that socket timeout and separately enforces
`read_timeout_s` as a wall-clock cap on the whole transfer, so both
budget knobs in the config do something real and neither is silently
ignored.
"""

from __future__ import annotations

import hashlib
import time
import urllib.error
import urllib.request
from dataclasses import dataclass
from pathlib import Path

CHUNK_SIZE = 1024 * 1024


class AcquireNetworkError(Exception):
    """A terminal network acquisition failure. Once raised, the caller
    must not retry with a different host/mirror/port -- see ADR-0003."""


@dataclass(frozen=True)
class DownloadResult:
    sha256: str
    size_bytes: int
    dest_path: str


def _is_policy_block(exc: Exception) -> bool:
    if isinstance(exc, urllib.error.HTTPError) and exc.code == 403:
        return True
    detail = str(exc)
    # The egress proxy reports a blocked CONNECT tunnel as a URLError
    # (the TLS tunnel itself is refused), not an HTTPError, but the
    # underlying message still carries "403" + "Forbidden".
    return "403" in detail and "Forbidden" in detail


def download_with_budget(
    url: str,
    dest_path: Path,
    *,
    connect_timeout_s: float,
    read_timeout_s: float,
    max_retries: int,
    retry_backoff_s: list[float],
    honor_retry_after_header: bool,
    max_download_bytes: int,
    sleep_fn=time.sleep,
) -> DownloadResult:
    attempt = 0
    last_exc: Exception | None = None
    while attempt <= max_retries:
        started_at = time.monotonic()
        try:
            req = urllib.request.Request(url, method="GET")
            with urllib.request.urlopen(req, timeout=connect_timeout_s) as resp:  # noqa: S310
                hasher = hashlib.sha256()
                size = 0
                with open(dest_path, "wb") as out:
                    while True:
                        if time.monotonic() - started_at > read_timeout_s:
                            raise AcquireNetworkError(
                                f"aborted: read_timeout_s ({read_timeout_s}s) exceeded while "
                                f"downloading {url}"
                            )
                        chunk = resp.read(CHUNK_SIZE)
                        if not chunk:
                            break
                        size += len(chunk)
                        if size > max_download_bytes:
                            raise AcquireNetworkError(
                                f"aborted: response exceeded max_download_bytes budget "
                                f"({max_download_bytes} bytes) while downloading {url}; "
                                "a country-sized download requires explicit human approval "
                                "and is never triggered automatically"
                            )
                        hasher.update(chunk)
                        out.write(chunk)
                return DownloadResult(sha256=hasher.hexdigest(), size_bytes=size, dest_path=str(dest_path))
        except AcquireNetworkError:
            if dest_path.exists():
                dest_path.unlink()
            raise
        except (urllib.error.HTTPError, urllib.error.URLError, TimeoutError, OSError) as exc:
            if dest_path.exists():
                dest_path.unlink()
            last_exc = exc
            if _is_policy_block(exc):
                raise AcquireNetworkError(
                    "blocked by organization egress policy (HTTP 403 on CONNECT/request); "
                    "never retried and no mirror/VPN/alternate port was attempted -- see "
                    f"docs/decisions/0003-osm-source-acquisition-blocked.md. Underlying error: {exc}"
                ) from exc
            retry_after_s: float | None = None
            if honor_retry_after_header and isinstance(exc, urllib.error.HTTPError) and exc.headers:
                raw = exc.headers.get("Retry-After")
                if raw is not None:
                    try:
                        retry_after_s = float(raw)
                    except ValueError:
                        retry_after_s = None
            if attempt >= max_retries:
                break
            backoff = (
                retry_backoff_s[min(attempt, len(retry_backoff_s) - 1)]
                if retry_backoff_s
                else float(2**attempt)
            )
            if retry_after_s is not None:
                backoff = max(backoff, retry_after_s)
            sleep_fn(backoff)
            attempt += 1
    raise AcquireNetworkError(
        f"network acquisition failed after {attempt + 1} attempt(s) against {url}: {last_exc}"
    ) from last_exc
