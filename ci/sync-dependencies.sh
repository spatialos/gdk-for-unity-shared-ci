#!/usr/bin/env bash

### This script should only be run on Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
  set -x
fi

cd "$(dirname "$0")/../"

DEPENDENCY_FILE="dependencies.pinned"

echo "--- Creating temp directory"
STAGING_DIR=$(mktemp -d)
mkdir -p "${STAGING_DIR}/macos"
mkdir -p "${STAGING_DIR}/win"
mkdir -p "${STAGING_DIR}/linux"
echo "${STAGING_DIR}"

cat "${DEPENDENCY_FILE}" | while read -r LINE; do
  DEST_FILEPATH=$(echo "${LINE}" | cut -d" " -f1)
  DOWNLOAD_URL=$(echo "${LINE}" | cut -d" " -f2)

  echo "--- Downloading ${DEST_FILEPATH}"
  curl "${DOWNLOAD_URL}" --output "${STAGING_DIR}/${DEST_FILEPATH}"
done

echo "--- Syncing to GCS"
BUCKET_ROOT="gs://io-internal-infra-dependencies-${BUILDKITE_AGENT_META_DATA_ENVIRONMENT}/spatialos/gdk-for-unity"
gsutil -m rsync -r "${STAGING_DIR}" "${BUCKET_ROOT}/"
