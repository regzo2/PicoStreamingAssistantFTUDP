#!/bin/bash

if [ `docker -v >/dev/null 2>&1 ; echo $?` -ne 0 ]; then
    echo "[e] Docker is not installed, or is currently stopped. Check https://docs.docker.com/get-docker/." >&2
    exit 1
fi

script_path=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
base_path=$(dirname "$script_path")

docker run -it --rm -v "$base_path":"/app" -e DOCKER_BUILD='true' mcr.microsoft.com/dotnet/sdk:7.0 bash /app/ci/build-images/build.sh