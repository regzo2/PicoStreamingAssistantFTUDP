#!/bin/bash

if [ `docker -v >/dev/null 2>&1 ; echo $?` -ne 0 ]; then
    echo "[e] Docker is not installed, or is currently stopped. Check https://docs.docker.com/get-docker/." >&2
    exit 1
fi

script_path=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
base_path=$(dirname "$script_path")

if [ ! -d "$base_path/artifacts" ]; then
    echo "[e] Project was not compiled; you have to run the build script first." >&2
    exit 1
fi

# metadata json needs to be named "module.json"
tmp_dir=`mktemp -d`
if [[ ! "$tmp_dir" || ! -d "$tmp_dir" ]]; then
    echo "[e] Could not create temp dir"
    exit 1
fi

cp "$base_path/metadata.json" "$tmp_dir/module.json"

# zip (no compression + no directories)
docker run -it --rm -v "$base_path":"/app" -v "$tmp_dir":"/metadata" joshkeegan/zip:latest zip -0 -j /app/artifacts/Pico4ProModule.zip "/app/artifacts/Pico4SAFTExtTrackingModule.dll" "/metadata/module.json"

# clear tmp dir
rm -rf "$tmp_dir"