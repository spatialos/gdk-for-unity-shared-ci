#!/usr/bin/env bash

### This script should only be run on Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

cd "$(dirname "$0")/../"

echo "## imp-ci group-start Setting up premerge :gear:"

docker build \
    --tag shared-ci-premerge \
    --file ./ci/docker/premerge.Dockerfile \
    .

echo "## imp-ci group-end Setting up premerge :gear:"

mkdir -p ./logs

echo "## imp-ci group-start Running premerge :running:"

docker run --rm \
    -v "$(pwd)"/logs:/var/logs \
    shared-ci-premerge

echo "## imp-ci group-end Running premerge :running:"
