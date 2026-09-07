#!/usr/bin/env bash
set -u

project_directory="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "${project_directory}/../.." && pwd)"
snapshot_directory="${project_directory}/Snapshots"
backup_directory="$(mktemp -d)"
mkdir -p "${snapshot_directory}"
cp -R "${snapshot_directory}/." "${backup_directory}/"
find "${snapshot_directory}" -name '*.received.txt' -delete

dotnet test --project "${project_directory}/AgentKit.Compatibility.Tests.csproj" --no-restore
initial_status=$?

shopt -s nullglob
received_files=("${snapshot_directory}"/*.received.txt)
if (( ${#received_files[@]} == 0 )); then
  if (( initial_status != 0 )); then
    echo "No fresh API snapshots were generated. If SnapshotCoverage reports stale verified files, delete exactly those reported files and rerun this command." >&2
  fi
  rm -rf "${backup_directory}"
  exit "${initial_status}"
fi

for received_file in "${received_files[@]}"; do
  verified_file="${received_file/.received.txt/.verified.txt}"
  cp "${received_file}" "${verified_file}"
  rm "${received_file}"
done

cd "${repository_root}"
dotnet test --project "${project_directory}/AgentKit.Compatibility.Tests.csproj" --no-restore
final_status=$?
if (( final_status != 0 )); then
  find "${snapshot_directory}" -mindepth 1 -delete
  cp -R "${backup_directory}/." "${snapshot_directory}/"
fi

rm -rf "${backup_directory}"
exit "${final_status}"
