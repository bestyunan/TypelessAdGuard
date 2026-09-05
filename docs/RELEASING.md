# Release locally, then publish

1. Build and run the tests from README. `package.ps1` creates binary and source ZIPs under `artifacts/releases/0.1.0-preview.1` using explicit file allowlists.
2. Inspect the manifests and SHA256SUMS.txt. Runtime logs, collected window data, personal paths, local shortcuts and internal troubleshooting notes must not be uploaded.
3. The prepared source folder can be imported into a new GitHub repository. Review the MIT license, README and workflow. No GitHub account or repository is configured in this project.
4. After cross-machine validation, create a Git tag matching the version, create a GitHub Release and upload the binary ZIP and SHA256SUMS.txt. GitHub's automatically generated source ZIP is not a runnable binary package.
5. Mark this release as a pre-release until independent Windows/Typeless installations have been tested. The workflow builds/test/packages and uploads CI artifacts; it does not publish Releases automatically.

The version is recorded in AssemblyInfo.cs, app.manifest, Launcher.cs, package.ps1 and README. Update these together for the next release. Compiling with a system compiler is repeatable but not guaranteed to produce byte-identical executables across compiler versions; record the shipped binary's SHA256.
