#!/usr/bin/env bash

### This script is used to setup Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

echo "--- Setting Android dependencies :android:"

source "$(dirname "$0")/pinned-tools.sh"

LOG_FILE=${1:-} # Optional argument - will log to console otherwise

RUN_UNITY_PATH="$(pwd)/tools/RunUnity/RunUnity.csproj"

pushd "$(dirname "$0")/../tools/AndroidDependencies"
    dotnet run -p "${RUN_UNITY_PATH}" -- \
        -projectPath "." \
        -batchmode \
        -quit \
        -logfile "${LOG_FILE}" \
        -executeMethod "UnityAndroidDependenciesSetter.Set" \
popd
