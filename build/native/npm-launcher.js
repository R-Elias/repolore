#!/usr/bin/env node
"use strict";
// Starts the native repolore executable from the platform package npm installed for this machine. This launcher is
// the npm channel's only addition: RepoLore itself still starts no other program.
const { spawnSync } = require("node:child_process");
const path = require("node:path");

const platformPackage = `@repolore-org/cli-${process.platform}-${process.arch}`;
let executable;
try {
  const packageDirectory = path.dirname(require.resolve(`${platformPackage}/package.json`));
  executable = path.join(packageDirectory, "bin", process.platform === "win32" ? "repolore.exe" : "repolore");
} catch {
  console.error(`repolore: no executable for ${process.platform}-${process.arch}: ${platformPackage} is not installed.`);
  console.error("repolore: builds exist for win32, linux and darwin on x64 and arm64; npm may have skipped optional dependencies.");
  process.exit(3);
}

const result = spawnSync(executable, process.argv.slice(2), { stdio: "inherit" });
if (result.error) {
  console.error(`repolore: could not start ${executable}: ${result.error.message}`);
  process.exit(3);
}
if (result.signal) {
  process.kill(process.pid, result.signal);
}
process.exit(result.status ?? 3);
