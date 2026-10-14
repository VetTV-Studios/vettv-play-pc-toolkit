# Releasing

Releases are built **only from a version tag** by `.github/workflows/release.yml`:

```
git tag v0.3.0
git push origin v0.3.0
```

The workflow runs tests, publishes the self-contained app (win-x64), builds the MSI with WiX v5, a portable zip, `SHA256SUMS.txt` and winget manifests, and creates a **draft** GitHub Release for you to review and publish.

## Code signing
Releases are **unsigned** unless both repo secrets exist:
- `SIGNING_CERT_PFX_BASE64`: base64 of a code-signing `.pfx` (`[Convert]::ToBase64String([IO.File]::ReadAllBytes('cert.pfx'))`)
- `SIGNING_CERT_PASSWORD`: its password

When they are present, `installer/build-installer.ps1` signs both exes and the MSI with SHA-256 and a timestamp. The PFX is written to the runner's temp folder and deleted afterwards. Without them the build still succeeds, `SIGNING.txt` says `signed=false`, and the release notes warn that SmartScreen will prompt.

No certificate exists yet. Options: an OV/EV code-signing certificate, or Azure Trusted Signing (this would need a workflow change).

## winget
After publishing the release, download `winget-manifests-<version>.zip` and submit it as a PR to `microsoft/winget-pkgs` (or use `wingetcreate`). Unsigned MSIs are accepted but may get extra validation.

## CI
`build.yml` also does an unsigned installer smoke build (version 0.0.0) on every push and uploads it as the `unsigned-installer-ci` artifact (kept 7 days) for testing. It is not a release.
