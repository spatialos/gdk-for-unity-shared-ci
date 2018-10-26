#!/usr/bin/env bash

### This script is used to setup Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -x -o pipefail

LOG_FILE=${1:-} # Optional argument - will log to console otherwise

pushd "$(dirname "$0")/../"
    source "scripts/pinned-tools.sh"
    source "scripts/profiling.sh"

    RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"
popd

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
