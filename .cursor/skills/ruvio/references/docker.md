# Docker

Port 6379, a password, and `everysec` durability. Image `salihcantekin/ruvio:latest` or `salihcantekin/ruvio:linux`.

## What the pages show

- A default process listens on `127.0.0.1:6379`. https://ruviodb.com/llms/setup.txt
- The Docker section builds a local tag `ruvio` and runs it with port 6379 and `RUVIO_REQUIREPASS`. It does not set `RUVIO_DURABILITY` or a volume. https://ruviodb.com/llms/setup.txt

```bash
docker build -t ruvio .
docker run --rm -p 6379:6379 -e RUVIO_REQUIREPASS=change-me ruvio
```

https://ruviodb.com/llms/setup.txt

- The image runs the server as a non-root user. https://ruviodb.com/llms/setup.txt
- The image `HEALTHCHECK` runs `ruvio-ctl ping` against `RUVIO_ADDR`'s port on 127.0.0.1. A `NOAUTH` reply counts as alive. https://ruviodb.com/llms/setup.txt
- `RUVIO_*` environment variables override the config file. A command-line flag overrides the variable. https://ruviodb.com/llms/configuration.txt
- `RUVIO_REQUIREPASS` sets `security.requirepass`. https://ruviodb.com/llms/configuration.txt
- Container images set `RUVIO_PROTECTED_MODE=false` because published ports are not loopback inside the namespace, so they need `RUVIO_REQUIREPASS` or an ACL password. https://ruviodb.com/llms/configuration.txt
- `RUVIO_ALLOW_UNAUTHENTICATED=true` is the explicit exception for a network that is already private. https://ruviodb.com/llms/configuration.txt
- `RUVIO_DURABILITY` sets `persistence.profile`: `memory`, `snapshot`, `everysec`, or `always`. The file default is `memory`. https://ruviodb.com/llms/configuration.txt
- `memory` keeps nothing on disk. `everysec` and `always` write a log under `--data-dir`. https://ruviodb.com/llms/setup.txt
- `everysec` acknowledges after the bytes are written to the kernel and runs `fdatasync` in the background every `sync_interval_ms` (default 1000). That interval is the loss window. https://ruviodb.com/llms/configuration.txt
- `always` ignores `sync_interval_ms` and `fdatasync`s a client batch before the replies are sent. https://ruviodb.com/llms/configuration.txt
- `snapshot` is one of the four `RUVIO_DURABILITY` values. This project does not use it. https://ruviodb.com/llms/configuration.txt
- `RUVIO_DATA_DIR` sets `persistence.data_dir`. The sample file value is `./data`. https://ruviodb.com/llms/configuration.txt
- WAL-backed profiles exclusively lock `<data_dir>/LOCK` before opening a listener. https://ruviodb.com/llms/configuration.txt
- Shard count, bind address, and durability cannot change under a live process. https://ruviodb.com/llms/configuration.txt
- `PUBLISH` is not written to the WAL, so none of these profiles stores live messages. https://ruviodb.com/llms/commands.txt
- Binary form of the same knobs: `./ruvio --requirepass change-me --data-dir ./data --durability everysec`. https://ruviodb.com/llms/setup.txt

## For this project

- This project sets `RUVIO_DURABILITY=everysec`.
