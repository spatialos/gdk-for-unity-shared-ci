#!/usr/bin/env bash

### This script should only be run on Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

cd "$(dirname "$0")/../"

DOTNET_URL="https://download.visualstudio.microsoft.com/download/pr/7430e32b-092b-4448-add7-2dcf40a7016d/1076952734fbf775062b48344d1a1587/dotnet-sdk-2.2.402-osx-x64.pkg"
DOTNET_FILE="dotnet-sdk-2.2.402-osx-x64.pkg"

BUCKET_ROOT="gs://io-internal-infra-dependencies-${BUILDKITE_AGENT_META_DATA_ENVIRONMENT}/spatialos/gdk-for-unity"

echo "--- Creating temp directory"
STAGING_DIR=$(mktemp -d)
echo "${STAGING_DIR}"

echo "--- Downloading dotnet installer"
curl "${DOTNET_URL}" --output "${STAGING_DIR}/${DOTNET_FILE}"

echo "--- Syncing to GCS"
gsutil -m rsync "${STAGING_DIR}" "${BUCKET_ROOT}/"
