#!/usr/bin/env bash

### This script is used to setup Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -x -o pipefail

source "$(dirname "$0")/pinned-tools.sh"
source "$(dirname "$0")/profiling.sh"

LOG_FILE=${1:-} # Optional argument - will log to console otherwise

if [[ -n "${BUILDKITE-}" ]]; then
    RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"
else
    RUN_UNITY_PATH="$(pwd)/.shared-ci/tools/RunUnity/RunUnity.csproj"
fi

markStartOfBlock "Setting Android dependencies"

pushd "$(dirname "$0")/../tools/AndroidDependencies"
    dotnet run -p "${RUN_UNITY_PATH}" -- \
        -projectPath "." \
        -batchmode \
        -quit \
        -logfile "${LOG_FILE}" \
        -executeMethod "UnityAndroidDependenciesSetter.Set" \
popd

markEndOfBlock "Setting Android dependencies"
