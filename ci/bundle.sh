#!/bin/bash

if [ `docker -v >/dev/null 2>&1 ; echo $?` -ne 0 ]; then
    echo "[e] Docker is not installed, or is currently stopped. Check https://docs.docker.com/get-docker/." >&2
    exit 1
fi

# TODO https://github.com/regzo2/PicoStreamingAssistantFTUDP/issues/15