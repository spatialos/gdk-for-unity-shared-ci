#!/usr/bin/env bash

# This is a generic build script for the GDK for Unity.
# The expected usage is as follows:
#   bash build.sh <unity_project_dir> <worker_type> <build_target> <log_file (optional)>

set -e -u -x -o pipefail

UNITY_PROJECT_DIR=$1
WORKER_TYPE=$2
BUILD_TARGET=$3
LOG_FILE=${4:-} # Optional argument - will log to console otherwise

pushd "$(dirname "$0")/../"
    source "scripts/pinned-tools.sh"
    source "scripts/profiling.sh"

    RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"
popd

markStartOfBlock "Building ${WORKER_TYPE} for ${BUILD_TARGET}"

pushd "${UNITY_PROJECT_DIR}"
    dotnet run -p "${RUN_UNITY_PATH}" -- \
        -projectPath "." \
        -batchmode \
        -quit \
        -logfile "${LOG_FILE}" \
        -executeMethod "Improbable.Gdk.BuildSystem.WorkerBuilder.Build" \
        +buildWorkerTypes "${WORKER_TYPE}" \
        +buildTarget "${BUILD_TARGET}"
popd

markEndOfBlock "Building ${WORKER_TYPE} for ${BUILD_TARGET}"
