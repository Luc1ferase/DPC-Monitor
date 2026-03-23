# Tag Release Workflow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a GitHub Actions workflow that triggers on version tags, builds a framework-dependent single-file Windows executable, renames it to `dpcMonitor.exe`, and uploads it to a GitHub Release.

**Architecture:** Keep the implementation contained to a single workflow file. Use the existing project publish path and GitHub CLI on the runner to create or update the Release and upload the generated executable.

**Tech Stack:** GitHub Actions, Windows runner, .NET SDK, GitHub CLI

---

### Task 1: Add Release Workflow

**Files:**
- Create: `.github/workflows/tag-release.yml`

- [ ] Add a workflow triggered by `push` tags matching `v*`
- [ ] Build on `windows-latest`
- [ ] Restore and publish `src/DpcMonitor.App/DpcMonitor.App.csproj`
- [ ] Rename output to `dpcMonitor.exe`
- [ ] Create or update the matching GitHub Release
- [ ] Upload `dpcMonitor.exe` as the Release asset

### Task 2: Verify Workflow Structure

**Files:**
- Modify: `.github/workflows/tag-release.yml`

- [ ] Review YAML for syntax and path correctness
- [ ] Confirm the workflow uses the existing release profile behavior
- [ ] Confirm the tag usage note for `v*` is ready to share with the user
