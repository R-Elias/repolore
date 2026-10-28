# site

The repolore.org website: `index.html` (install table, download links, checksums), `install.sh` and `install.ps1`. There is no build step; the `website` job in `publish` copies these files to GitHub Pages.

- **The version is filled in at deploy.** `__VERSION__` appears in all three files, and the job replaces it with the published version and fails if any is left. The site therefore shows the latest stable release, and a prerelease never reaches it. Keep the placeholder when editing; do not hard-code a version.
- **The scripts verify before installing.** They download the archive from the GitHub release, check its SHA-256 against `repolore-<version>.sha256`, and refuse a mismatch. Overrides: `REPOLORE_VERSION`, `REPOLORE_INSTALL_DIR` and `REPOLORE_DOWNLOAD_BASE` (an internal mirror). The CI install test serves the release files from `127.0.0.1` through that last one, so keep it working.
- **Domain:** the custom domain is a Pages setting; `site/CNAME` is not what configures it for an Actions deployment. At the registrar the records must be DNS-only: a proxied record stops GitHub from issuing the HTTPS certificate.
- **Cloudflare Pages is planned** for later; the switch is the job's last three steps, as written in [distribution](../../roadmap/distribution.md).
- Nothing tests the page itself. After a release, open repolore.org, check the version, and run the install script in a scratch directory.
