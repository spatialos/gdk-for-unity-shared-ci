#!/usr/bin/env bash

### This script should only be run on Improbable's internal build machines.
### If you don't work at Improbable, this may be interesting as a guide to what software versions we use for our
### automation, but not much more than that.

set -e -u -o pipefail

if [[ -n "${DEBUG-}" ]]; then
    set -x
fi

cd "$(dirname "$0")/../"

source "scripts/pinned-tools.sh"

DEPENDENCY_FILE="dependencies.pinned"

traceStart "Creating temp directory"
    STAGING_DIR=$(mktemp -d)
    mkdir -p "${STAGING_DIR}/macos"
    mkdir -p "${STAGING_DIR}/win"
    mkdir -p "${STAGING_DIR}/linux"
    echo "${STAGING_DIR}"
traceEnd

cat "${DEPENDENCY_FILE}" | while read -r LINE; do
    DEST_FILEPATH=$(echo "${LINE}" | cut -d" " -f1)
    DOWNLOAD_URL=$(echo "${LINE}" | cut -d" " -f2)

    traceStart "Downloading ${DEST_FILEPATH}"
        curl "${DOWNLOAD_URL}" --output "${STAGING_DIR}/${DEST_FILEPATH}"
    traceEnd
done

traceStart "Syncing to GCS"
    BUCKET_ROOT="gs://io-internal-infra-dependencies-${BUILDKITE_AGENT_META_DATA_ENVIRONMENT}/spatialos/gdk-for-unity"
    gsutil -m rsync -r "${STAGING_DIR}" "${BUCKET_ROOT}/"
traceEnd
