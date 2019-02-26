#!/usr/bin/env bash

# This is a generic build script for the GDK for Unity.
# The expected usage is as follows:
#   bash build.sh <unity_project_dir> <worker_type> <build_target> <scripting_backend> <log_file (optional)>

set -e -u -x -o pipefail

source "$(dirname "$0")/pinned-tools.sh"
source "$(dirname "$0")/profiling.sh"

UNITY_PROJECT_DIR=$1
WORKER_TYPE=$2
BUILD_TARGET=$3
SCRIPTING_BACKEND=$4
LOG_FILE=${5:-} # Optional argument - will log to console otherwise

# The asset cache ip cannot be hardcoded and so is stored in an environment variable on the build agent.
# This is bash shorthand syntax for if-else predicated on the existance of the environment variable
# where the else branch assigns an empty string.
#   i.e. -
#   if [ -z ${UNITY_ASSET_CACHE_IP} ]; then
#       ASSET_CACHE_ARG="-CacheServerIPAddress ${UNITY_ASSET_CACHE_IP}"
#   else
#       ASSET_CACHE_ARG=""
#   fi

ASSET_CACHE_ARG=${UNITY_ASSET_CACHE_IP:+-CacheServerIPAddress "${UNITY_ASSET_CACHE_IP}"}

if [[ -n "${BUILDKITE-}" ]]; then
    RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"
else
    RUN_UNITY_PATH="$(pwd)/.shared-ci/tools/RunUnity/RunUnity.csproj"
fi

markStartOfBlock "Building ${WORKER_TYPE} for ${BUILD_TARGET} and ${SCRIPTING_BACKEND}"

pushd "${UNITY_PROJECT_DIR}"
    rm -rf ./build/worker/
    dotnet run -p "${RUN_UNITY_PATH}" -- \
        -projectPath "." \
        -batchmode \
        -quit \
        -logfile "${LOG_FILE}" \
        -executeMethod "Improbable.Gdk.BuildSystem.WorkerBuilder.Build" \
        ${ASSET_CACHE_ARG} \
        +buildWorkerTypes "${WORKER_TYPE}" \
        +buildTarget "${BUILD_TARGET}" \
        +scriptingBackend "${SCRIPTING_BACKEND}"
popd

markEndOfBlock "Building ${WORKER_TYPE} for ${BUILD_TARGET} and ${SCRIPTING_BACKEND}"
