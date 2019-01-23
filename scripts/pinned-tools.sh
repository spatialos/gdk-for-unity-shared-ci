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

function checkForAllPlaybackEngines() {
  IMPROBABLE_UNITY_VERSIONED=$1

  if isWindows; then
    PLAYBACK_ENGINE_ROOT="$IMPROBABLE_UNITY_VERSIONED/Editor/Data/PlaybackEngines"
  elif isMacOS; then
    PLAYBACK_ENGINE_ROOT="$IMPROBABLE_UNITY_VERSIONED/PlaybackEngines"
  fi

  IMPROBABLE_UNITY_ROOT=$2

  [ ! -d "$PLAYBACK_ENGINE_ROOT/LinuxStandaloneSupport" ]
  LINUX_ERROR=$?

  [ ! -d "$PLAYBACK_ENGINE_ROOT/AndroidPlayer" ]
  ANDROID_ERROR=$?

  [ ! -d "$PLAYBACK_ENGINE_ROOT/iOSSupport" ]
  IOS_ERROR=$?

  [ ! -d "$PLAYBACK_ENGINE_ROOT/WindowsStandaloneSupport" ]
  WINDOWS_MONO_ERROR=$?

  # Assume installed until we know for sure it isn't, so that we only flag up modules relevant for the current platform.
  MAC_IL2CPP_ERROR=1
  MAC_MONO_ERROR=1
  WINDOWS_IL2CPP_ERROR=1

  # Only check for Mac IL2CPP support on Mac agents, as Unity do not provide Mac-IL2CPP support for Windows.
  # Only check for Mac standalone support on Windows because it is installed within the Unity app package itself on MacOS.
  # Only check for Windows IL2CPP support on Windows agents, as Unity do not provide Windows-IL2CPP support for Mac.
  if isMacOS; then
    [ ! -d "$IMPROBABLE_UNITY_VERSIONED/Unity.app/Contents/PlaybackEngines/MacStandaloneSupport/Variations/macosx64_development_il2cpp" ]
    MAC_IL2CPP_ERROR=$?
  else
    [ ! -d "$PLAYBACK_ENGINE_ROOT/MacStandaloneSupport" ]
    MAC_MONO_ERROR=$?

    [ ! -d "$PLAYBACK_ENGINE_ROOT/WindowsStandaloneSupport/Variations/win64_development_il2cpp" ]
    WINDOWS_IL2CPP_ERROR=$?
  fi

  if [ $LINUX_ERROR -eq 0 ] || \
     [ $ANDROID_ERROR -eq 0 ] || \
     [ $IOS_ERROR -eq 0 ] || \
     [ $WINDOWS_MONO_ERROR -eq 0 ] || \
     [ $MAC_IL2CPP_ERROR -eq 0 ] || \
     [ $MAC_MONO_ERROR -eq 0 ] || \
     [ $WINDOWS_IL2CPP_ERROR -eq 0 ]
  then
    echo "Could not find all playback engines."
    return 1
  else
    echo "All playback engines are already installed in: $PLAYBACK_ENGINE_ROOT."
    return 0
  fi
}

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
    echo "Example usage: 'setAssemblyName my_prefix'"
    exit 1
  fi

  ASSEMBLY_NAME="${1}_${GIT_HASH}"
}
