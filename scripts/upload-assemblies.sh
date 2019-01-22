#!/usr/bin/env bash

# This is a generic script for uploading assemblies in the GDK for Unity.
# Note that this script needs to be called from a SpatialOS project with Git VCS.
# The expected usage is as follows:
#   bash upload-assemblies.sh <assembly_name_prefix>

set -e -u -x -o pipefail

if [ "$#" -ne 1 ]; then
    # Print the comments above.
    sed -n '3,6p' < $0
    exit 1
fi

pushd "$(dirname "$0")/../"
    source "scripts/pinned-tools.sh"
    source "scripts/profiling.sh"
popd

PREFIX=$1

markStartOfBlock "Uploading assemblies"

# Get first 8 characters of current git hash.
GIT_HASH="$(git rev-parse HEAD | cut -c1-8)"

spatial cloud upload "${PREFIX}_${GIT_HASH}" --log_level=debug

markEndOfBlock "Uploading assemblies"
