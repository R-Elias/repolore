# Unreleased (next version)

Draft notes. When the next version is cut, rename this file to `<version>.md` (the release workflow refuses a version without its own notes file).

- Security: `SECURITY.md` with a private reporting route.
- Package metadata now carries the copyright line; the readme says "no author signature" and names the copyright holder.
- Each release now publishes a CycloneDX SBOM and a SHA-256 checksum file for the exact package and SBOM.
- User guide: supported environments, how the privacy claim is evidenced, and how to verify a release.
- Author signing is not planned for 1.0.0 (maintainer decision of 2026-10-24); releases are verified through Trusted Publishing, NuGet.org's repository signature, the checksum, and the SBOM.
