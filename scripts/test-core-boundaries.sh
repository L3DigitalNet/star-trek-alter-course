#!/usr/bin/env bash
# Prove the actual Core banned-API configuration rejects ambient authority and
# still permits persistence filename entropy and nonauthoritative tooling.
# Probes are compiled only; their wall-clock sleeps and delays never execute.
# Usage: ./scripts/test-core-boundaries.sh
# Requirements: Bash, GNU coreutils/awk/grep, and the resolved repository .NET SDK.
# Existing Core package pins and lock data are reused, so probes add no dependencies.

set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
readonly root
fixture="$(mktemp -d "${root}/.core-boundary-probes.XXXXXX")"
readonly fixture
trap 'rm -rf -- "${fixture}"' EXIT
dotnet_dir="$("${root}/scripts/resolve-dotnet.sh")"
readonly dotnet_dir
export PATH="${dotnet_dir}:${PATH}"
export MSBUILDDISABLENODEREUSE=1

fail() {
  printf 'test-core-boundaries.sh: %s\n' "$*" >&2
  exit 1
}

# Keeping the fixture below the root preserves the real props, central package
# versions, editorconfig, and banned-symbol file instead of duplicating enforcement.
cp "${root}/src/AlterCourse.Core/AlterCourse.Core.csproj" "${fixture}/AlterCourse.Core.csproj"
cp "${root}/src/AlterCourse.Core/packages.lock.json" "${fixture}/packages.lock.json"
cat > "${fixture}/Probe.cs" << 'CS'
namespace BoundaryFixtures;

internal static class Probe
{
    internal static void Execute(System.TimeProvider time)
    {
        _ = System.DateTime.Now; // banned
        _ = System.DateTime.UtcNow; // banned
        _ = System.DateTime.Today; // banned
        _ = System.DateTimeOffset.Now; // banned
        _ = System.DateTimeOffset.UtcNow; // banned
        _ = new System.Random(); // banned
        _ = System.Random.Shared; // banned
        _ = System.Environment.TickCount; // banned
        _ = System.Environment.TickCount64; // banned
        _ = System.Guid.NewGuid(); // banned
        System.Threading.Thread.Sleep(1); // banned
        System.Threading.Thread.Sleep(System.TimeSpan.Zero); // banned
        _ = System.Threading.Tasks.Task.Delay(1); // banned
        _ = System.Threading.Tasks.Task.Delay(System.TimeSpan.Zero); // banned
        _ = System.Threading.Tasks.Task.Delay(1, System.Threading.CancellationToken.None); // banned
        _ = System.Threading.Tasks.Task.Delay(System.TimeSpan.Zero, System.Threading.CancellationToken.None); // banned
        _ = System.Threading.Tasks.Task.Delay(System.TimeSpan.Zero, time); // banned
        _ = System.Threading.Tasks.Task.Delay(System.TimeSpan.Zero, time, System.Threading.CancellationToken.None); // banned
        _ = time.GetUtcNow(); // banned
        _ = time.GetLocalNow(); // banned
        _ = time.GetTimestamp(); // banned
        _ = time.GetElapsedTime(0); // banned
        using System.Threading.ITimer providerTimer = time.CreateTimer(_ => { }, null, System.TimeSpan.Zero, System.Threading.Timeout.InfiniteTimeSpan); // banned
        using var threadingTimer = new System.Threading.Timer(_ => { }); // banned
        using var periodicTimer = new System.Threading.PeriodicTimer(System.TimeSpan.FromSeconds(1)); // banned
        using var suppliedClockPeriodicTimer = new System.Threading.PeriodicTimer(System.TimeSpan.FromSeconds(1), time); // banned
        using var componentTimer = new System.Timers.Timer(); // banned
        _ = System.Diagnostics.Stopwatch.GetTimestamp(); // banned
        _ = System.Diagnostics.Stopwatch.StartNew(); // banned
        _ = System.Security.Cryptography.RandomNumberGenerator.GetInt32(10); // banned
        _ = System.Security.Cryptography.RandomNumberGenerator.GetBytes(4); // banned
    }
}
CS
cp "${fixture}/Probe.cs" "${fixture}/AmbientProbe.txt"

dotnet restore "${fixture}/AlterCourse.Core.csproj" --locked-mode --disable-build-servers > "${fixture}/restore.log" 2>&1 || {
  cat "${fixture}/restore.log" >&2
  fail 'probe restore failed'
}

if dotnet build "${fixture}/AlterCourse.Core.csproj" --no-restore --disable-build-servers > "${fixture}/negative.log" 2>&1; then
  fail 'authoritative ambient APIs compiled successfully'
fi
if grep ': error ' "${fixture}/negative.log" | grep -v ': error RS0030:'; then
  fail 'negative fixture has unrelated compiler errors'
fi
# Each marked invocation must independently produce the banned-API diagnostic;
# an unrelated compiler or analyzer failure cannot satisfy this negative proof.
missing_lines=()
while read -r line; do
  if ! grep -Eq "Probe.cs\(${line},[0-9]+\): error RS0030:" "${fixture}/negative.log"; then
    missing_lines+=("${line}")
  fi
done < <(awk '/\/\/ banned/ { print NR }' "${fixture}/Probe.cs")
if [[ "${#missing_lines[@]}" -gt 0 ]]; then
  cat "${fixture}/negative.log" >&2
  printf 'Missing RS0030 for Probe.cs line %s\n' "${missing_lines[@]}" >&2
  fail 'ambient API enforcement is incomplete'
fi
printf 'Core rejects every ambient authority probe.\n'

cat > "${fixture}/Probe.cs" << 'CS'
namespace BoundaryFixtures;

internal static class Probe
{
    internal static string TemporarySavePath(string directory, System.DateTimeOffset suppliedTimestamp)
        => System.IO.Path.Combine(directory, suppliedTimestamp.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) + System.IO.Path.GetRandomFileName());

    internal static (System.DateTime, System.DateTimeOffset, System.TimeSpan) TransformSuppliedValues(
        System.DateTime timestamp, System.TimeSpan duration, System.TimeProvider provider)
        => (timestamp.Add(duration), new System.DateTimeOffset(timestamp, System.TimeSpan.Zero), provider.GetElapsedTime(0, 1));
}
CS
if ! dotnet build "${fixture}/AlterCourse.Core.csproj" --no-restore --disable-build-servers > "${fixture}/allowed.log" 2>&1; then
  cat "${fixture}/allowed.log" >&2
  fail 'legitimate Core persistence APIs were rejected'
fi
printf 'Core permits temporary save filename entropy and supplied metadata time.\n'

# The renamed project receives the same analyzers but must not inherit Core's
# AdditionalFiles. This guards the project-scope condition in Directory.Build.props.
mv "${fixture}/AlterCourse.Core.csproj" "${fixture}/BoundaryTools.csproj"
sed -i '/<PackageReference Include="JsonSchema.Net"/a\        <PackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" PrivateAssets="all" />' "${fixture}/BoundaryTools.csproj"
cp "${fixture}/AmbientProbe.txt" "${fixture}/Probe.cs"
if ! dotnet restore "${fixture}/BoundaryTools.csproj" --locked-mode --disable-build-servers > "${fixture}/tools-restore.log" 2>&1; then
  cat "${fixture}/tools-restore.log" >&2
  fail 'nonauthoritative probe restore failed'
fi
if ! dotnet build "${fixture}/BoundaryTools.csproj" --no-restore --disable-build-servers > "${fixture}/tools.log" 2>&1; then
  cat "${fixture}/tools.log" >&2
  fail 'nonauthoritative tooling ambient APIs were rejected'
fi
printf 'Nonauthoritative tooling permits every ambient API probe.\n'
