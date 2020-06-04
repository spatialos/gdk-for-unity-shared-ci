#!/usr/bin/env bash

set -e -u -o pipefail

# This is a generic lint script for the GDK for Unity
#
# Usage: lint.sh <relative-path-to-dir> <optional-args>

if [[ "$#" -eq 0 ]]; then
    echo "Expected usage: <path-to-lint.sh> <relative-path-to-target-dir> <other-args>"
    exit 1
fi

source "$(dirname "$0")/pinned-tools.sh"

echo "## imp-ci group-start Building Docker image :docker:"

pushd "$(dirname "$0")/.."
    docker build --file ci/docker/lint/Dockerfile \
        --tag gdk-linter \
        .
popd

echo "## imp-ci group-end Building Docker image :docker:"

if isWindows; then
    CURRENT_DIR="$(pwd -W)"
else
    CURRENT_DIR="$(pwd)"
fi

echo "## imp-ci group-start Linting ${1} :lint-roller:"

docker run --rm \
    -v "${CURRENT_DIR}:/project" \
    gdk-linter -f "$@"

echo "## imp-ci group-end Linting ${1} :lint-roller:"
