#!/usr/bin/env bash

# This is a generic script for uploading assemblies in the GDK for Unity.
# Note that this script needs to be called from a SpatialOS project with Git VCS.
# The expected usage is as follows:
#   bash upload-assemblies.sh <assembly_name>

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

ASSEMBLY=$1

markStartOfBlock "Uploading assemblies"

spatial cloud upload "${ASSEMBLY}" --log_level=debug

markEndOfBlock "Uploading assemblies"
