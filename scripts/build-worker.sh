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
#   IOS_TARGET_SDK
#   ACCELERATOR_ENDPOINT

if [[ -n "${DEBUG-}" ]]; then
    set -x
fi

source "$(dirname "$0")/pinned-tools.sh"

pushd "$(dirname "$0")/../"
    ACCELERATOR_ARGS=$(getAcceleratorArgs)

    if [[ -n ${BUILD_TARGET_FILTER-} ]]; then
        BLOCK_MESSAGE="Building ${WORKER_TYPE} for ${BUILD_ENVIRONMENT} on ${BUILD_TARGET_FILTER} using ${SCRIPTING_BACKEND}"
        LOG_FILE="$(pwd)/../logs/${WORKER_TYPE}-${BUILD_ENVIRONMENT}-${BUILD_TARGET_FILTER// /-}-${SCRIPTING_BACKEND}.log"
        BUILD_TARGET_FILTER_ARG="+buildTargetFilter ${BUILD_TARGET_FILTER}"
    else
        BLOCK_MESSAGE="Building ${WORKER_TYPE} for ${BUILD_ENVIRONMENT} using ${SCRIPTING_BACKEND}"
        LOG_FILE="$(pwd)/../logs/${WORKER_TYPE}-${BUILD_ENVIRONMENT}-${SCRIPTING_BACKEND}.log"
        BUILD_TARGET_FILTER_ARG=""
    fi

    TARGET_IOS_SDK_ARG=""
    if [[ -n ${TARGET_IOS_SDK-} ]]; then
        TARGET_IOS_SDK_ARG="+targetiOSSdk ${TARGET_IOS_SDK}"
    fi

    RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"

    pushd "$(pwd)/../workers/unity"
        traceStart "${BLOCK_MESSAGE} :hammer_and_wrench:"
            dotnet run -p "${RUN_UNITY_PATH}" -- \
                -projectPath "." \
                -batchmode \
                -nographics \
                -quit \
                -logfile "${LOG_FILE}" \
                -executeMethod "Improbable.Gdk.BuildSystem.WorkerBuilder.Build" \
                "${ACCELERATOR_ARGS}" \
                +buildWorkerTypes "${WORKER_TYPE}" \
                +buildEnvironment "${BUILD_ENVIRONMENT}" \
                +scriptingBackend "${SCRIPTING_BACKEND}" \
                "${TARGET_IOS_SDK_ARG}" \
                "${BUILD_TARGET_FILTER_ARG}"

            if isMacOS && [[ "${BUILD_TARGET_FILTER-}" =~ "ios" ]]; then
                traceStart "Building XCode Project :xcode:"
                    dotnet run -p "${RUN_UNITY_PATH}" -- \
                        -projectPath "." \
                        -batchmode \
                        -nographics \
                        -quit \
                        -logfile "$(pwd)/../../logs/${WORKER_TYPE}-${BUILD_ENVIRONMENT}-xcode-build.log" \
                        -executeMethod "Improbable.Gdk.Mobile.iOSUtils.Build" \
                        "${ACCELERATOR_ARGS}"
                traceEnd
            fi
        traceEnd
    popd
popd
