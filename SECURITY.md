# Security policy

RepoLore is a local, offline tool maintained by one person. Please report suspected vulnerabilities privately.

## Supported versions

Only the latest published version of `RepoLore.Cli` receives fixes. A fix is always a newer version, never a change to a published one; the preview and the release candidates receive no fixes of their own.

## How to report

Use GitHub private vulnerability reporting: open the repository's **Security** tab and choose **Report a vulnerability**. Please do not open a public issue for a suspected vulnerability.

Include the output of `repolore version`, your operating system and .NET SDK version, the steps to reproduce, and the impact: which file or state could be read, changed, or lost.

## What to expect

This is best-effort work by one maintainer. There is no guaranteed response time and no paid support. A report will be acknowledged, assessed, and, if it is a vulnerability, fixed in a new version with the reporter credited if they wish.

## Scope

In scope: the `RepoLore.Cli` package, for example path handling and escapes from the repository, unintended file writes or deletions, loss of recoverable history, and anything that makes the tool perform a network or process operation (it must perform none).

Out of scope: what editors, coding agents, or other programs do with content they read from a RepoLore knowledge base (RepoLore cannot prevent them from transmitting it), and the behavior of NuGet.org, GitHub, or the .NET SDK and runtime themselves.
