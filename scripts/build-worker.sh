#!/usr/bin/env bash
set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

source "$(dirname "$0")/pinned-tools.sh"

echo "Building for: ${WORKER_TYPE} ${BUILD_TARGET} ${SCRIPTING_TYPE}"

pushd "$(dirname "$0")/../"
  if [[ ${WORKER_TYPE} == "AndroidClient" ]]; then
      scripts/prepare-unity-mobile.sh "$(pwd)/../logs/PrepareUnityMobile.log"
  fi

  if [[ ${WORKER_TYPE} == "iOSClient" ]]; then
      if ! isMacOS; then
          echo "I can't build for iOS!"
          exit 1
      fi
  fi

  LOG_LOCATION="$(pwd)/../logs/${WORKER_TYPE}-${BUILD_TARGET}-${SCRIPTING_TYPE}.log"

  scripts/build.sh "$(pwd)/../workers/unity" ${WORKER_TYPE} ${BUILD_TARGET} ${SCRIPTING_TYPE} "${LOG_LOCATION}"
  spatial prepare-for-run
popd
