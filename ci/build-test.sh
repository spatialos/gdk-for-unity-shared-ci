#!/usr/bin/env bash

### This script should only be run on Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
    set -x
fi

cd "$(dirname "$0")/../"

source "scripts/pinned-tools.sh"

traceStart "Setting up premerge :gear:"
    docker build \
        --tag shared-ci-premerge \
        --file ./ci/docker/premerge.Dockerfile \
        .
traceEnd

mkdir -p ./logs

traceStart "Running premerge :running:"
    docker run --rm \
        -v "$(pwd)"/logs:/var/logs \
        shared-ci-premerge
traceEnd
