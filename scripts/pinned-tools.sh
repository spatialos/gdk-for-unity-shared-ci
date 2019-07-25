#!/usr/bin/env bash
error() {
   local SOURCE_FILE=$1
   local LINE_NO=$2
   echo "ERROR: ${SOURCE_FILE}(${LINE_NO}):"
}

function isLinux() {
  [[ "$(uname -s)" == "Linux" ]];
}

function isMacOS() {
  [[ "$(uname -s)" == "Darwin" ]];
}

function isWindows() {
  ! ( isLinux || isMacOS );
}

function cleanUnity() {
  rm -rf "${1}/Library"
  rm -rf "${1}/Temp"
}

echo "--- Sourcing pinned tools :round_pushpin:"

# Ensure for the Mac TC agents that dotnet is on the path.
if isMacOS; then
  if ! which dotnet; then
    export PATH="${PATH}:/usr/local/share/dotnet/"
  fi
fi

# Print the .NETCore version to aid debugging,
# as well as ensuring that later calls to the tool don't print the welcome message on first run.
dotnet --version

DOTNET_VERSION="$(dotnet --version)"

if isWindows; then
  export MSBuildSDKsPath="${PROGRAMFILES}/dotnet/sdk/${DOTNET_VERSION}/Sdks"
fi

# Creates an assembly name based on an argument (used as a prefix) and the current Git hash.
function setAssemblyName() {
  # Get first 8 characters of current git hash.
  GIT_HASH="$(git rev-parse HEAD | cut -c1-8)"

  if [ "$#" -ne 1 ]; then
    echo "'setAssemblyName' expects only one argument."
    echo "Example usage: 'setAssemblyName <assembly prefix>'"
    exit 1
  fi

  ASSEMBLY_NAME="${1}_${GIT_HASH}"
}

# Uploads an assembly, given an assembly prefix and a project name
function uploadAssembly() {
  if [ "$#" -ne 2 ]; then
    echo "'uploadAssembly' expects two arguments."
    echo "Example usage: 'uploadAssembly <assembly prefix> <project name>'"
    exit 1
  fi

  setAssemblyName "${1}"

  echo "Uploading assembly"
  spatial cloud upload "${ASSEMBLY_NAME}" --log_level=debug --force --enable_pre_upload_check=false --project_name="${2}"
}

function isDocsBranch() {
  if [[ -n "${BUILDKITE_BRANCH-}" ]]; then
    BRANCH="${BUILDKITE_BRANCH}"
  else
    BRANCH=$(git branch | sed -n -e 's/^\* \(.*\)/\1/p')
  fi

  if [[ "${BRANCH}" == docs/* ]]; then
    return 0
  fi
  return 1
}
