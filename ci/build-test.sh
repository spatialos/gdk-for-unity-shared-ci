#!/usr/bin/env bash

set -e -u -o -x pipefail

cd "$(dirname "$0")/../"

TOOLS_TEST_RESULTS_FILES="${PROJECT_DIR}/logs/tools-test-results.xml"

markStartOfBlock "Tools Testing"

dotnet test --logger:"nunit;LogFilePath=${TOOLS_TEST_RESULTS_FILES}" "tools/DocsLinter/DocsLinter.csproj"
TOOLS_TEST_RESULT=$?

markEndOfBlock "Tools Testing"

if [ $TOOLS_TEST_RESULT -ne 0 ]; then
    >&2 echo "Tools Tests failed. Please check the file ${TOOLS_TEST_RESULTS_FILES} for more information."
    exit 1
fi