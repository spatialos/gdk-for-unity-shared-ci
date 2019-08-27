#!/usr/bin/env bash
set -e -u -o pipefail

# This is a generic build script for the GDK for Unity.
#
# Expected environment variables:
#   WORKER_TYPE
#   BUILD_ENVIRONMENT
#   SCRIPTING_BACKEND
#
# Optional environment variables:
#   BUILD_TARGET_FILTER

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

source "$(dirname "$0")/pinned-tools.sh"

pushd "$(dirname "$0")/../"
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

    if [[ -n ${BUILD_TARGET_FILTER-} ]]; then
        BLOCK_MESSAGE="Building ${WORKER_TYPE} for ${BUILD_ENVIRONMENT} on ${BUILD_TARGET_FILTER} using ${SCRIPTING_BACKEND}"
        LOG_FILE="$(pwd)/../logs/${WORKER_TYPE}-${BUILD_ENVIRONMENT}-${BUILD_TARGET_FILTER}-${SCRIPTING_BACKEND}.log"
        BUILD_TARGET_FILTER_ARG="+buildTargetFilter ${BUILD_TARGET_FILTER}"
    else
        BLOCK_MESSAGE="Building ${WORKER_TYPE} for ${BUILD_ENVIRONMENT} using ${SCRIPTING_BACKEND}"
        LOG_FILE="$(pwd)/../logs/${WORKER_TYPE}-${BUILD_ENVIRONMENT}-${SCRIPTING_BACKEND}.log"
        BUILD_TARGET_FILTER_ARG=""
    fi

    RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"

    pushd "$(pwd)/../workers/unity"
        echo "--- ${BLOCK_MESSAGE} :hammer_and_wrench:"

        dotnet run -p "${RUN_UNITY_PATH}" -- \
            -projectPath "." \
            -batchmode \
            -quit \
            -logfile "${LOG_FILE}" \
            -executeMethod "Improbable.Gdk.BuildSystem.WorkerBuilder.Build" \
            "${ASSET_CACHE_ARG}" \
            +buildWorkerTypes "${WORKER_TYPE}" \
            +buildEnvironment "${BUILD_ENVIRONMENT}" \
            +scriptingBackend "${SCRIPTING_BACKEND}" \
            "${BUILD_TARGET_FILTER_ARG}"
    popd
popd
