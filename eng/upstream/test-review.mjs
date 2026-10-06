#!/usr/bin/env node
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '../..');
const prepare = resolve(root, 'eng/upstream/prepare-review.mjs');
const publish = resolve(root, 'eng/upstream/publish-review-branch.sh');
const openPr = resolve(root, 'eng/upstream/open-review-pr.sh');
const temporary = mkdtempSync(join(tmpdir(), 'runic-validation-upstream-review-'));
function run(command, args, cwd, env = {}) { return execFileSync(command, args, { cwd, env: { ...process.env, ...env }, encoding: 'utf8', stdio: 'pipe' }).trim(); }
function git(args, repo) { return run('git', args, repo); }
function commit(repo, message) { git(['add', '.'], repo); git(['-c', 'user.name=Test', '-c', 'user.email=test@example.invalid', 'commit', '-m', message], repo); return git(['rev-parse', 'HEAD'], repo); }
function inventory(file, pin) { writeFileSync(file, JSON.stringify({ requestedUpstreamCommit: pin, observedUpstreamMainCommit: pin, collectedAt: '2026-10-01T00:00:00Z', counts: { issues: 0, pullRequests: 0, open: 0, total: 0 }, assessmentPolicy: 'fresh' }) + '\n'); }
function prepareSnapshot(repo, base, upstream, destination) { const data = join(temporary, `inventory-${Math.random()}.json`); inventory(data, upstream); return JSON.parse(run('node', [prepare, '--repo', repo, '--base', base, '--upstream', upstream, '--month', '2026-10', '--inventory', data, '--output', destination], root)); }
try {
  const repo = join(temporary, 'repo'); run('git', ['init', '--initial-branch=main', repo]);
  writeFileSync(join(repo, 'shared.txt'), 'root\n'); const initial = commit(repo, 'initial');
  git(['switch', '-c', 'upstream-main'], repo); writeFileSync(join(repo, 'upstream.txt'), 'candidate\n'); const candidate = commit(repo, 'upstream candidate');
  git(['switch', '-c', 'runic-main', initial], repo); writeFileSync(join(repo, 'runic.txt'), 'fork\n'); const base = commit(repo, 'linear inherited fork');
  git(['update-ref', 'refs/remotes/upstream/main', candidate], repo);
  const snapshot = join(temporary, 'snapshot'); const result = prepareSnapshot(repo, base, candidate, snapshot);
  assert.equal(result.provenance, 'inherited-shared-merge-base');
  assert.equal(result.merge, 'clean');
  const manifest = JSON.parse(readFileSync(join(snapshot, 'manifest.json'), 'utf8'));
  assert.equal(manifest.previousUpstreamProvenance.commit, initial);
  assert.equal(manifest.previousUpstreamProvenance.status, 'inherited-shared-merge-base');
  assert.throws(() => prepareSnapshot(repo, base, candidate, snapshot), /Refusing to overwrite/);

  const remote = join(temporary, 'origin.git'); run('git', ['init', '--bare', remote]); git(['remote', 'add', 'origin', remote], repo); git(['push', '--set-upstream', 'origin', 'runic-main'], repo);
  const branch = `review/upstream/2026-10-${base.slice(0, 12)}-${candidate.slice(0, 12)}`;
  const fakeGit = join(temporary, 'git-wrapper'); writeFileSync(fakeGit, `#!/usr/bin/env bash
if [[ " $* " == *" remote get-url --push --all origin "* || " $* " == *" remote get-url origin "* ]]; then printf '%s\\n' 'https://github.com/Runic-Artifex/ReactiveUI.Validation.git'; exit 0; fi
exec git "$@"
`, { mode: 0o755 });
  const isolated = { GIT_BIN: fakeGit, GIT_CONFIG_GLOBAL: '/dev/null', GIT_CONFIG_NOSYSTEM: '1' };
  const publishArgs = [publish, '--repo', repo, '--base', base, '--upstream', candidate, '--branch', branch, '--snapshot', snapshot];
  const created = run('bash', publishArgs, root, isolated); assert.match(created, /^status=created/);
  const existing = run('bash', publishArgs, root, isolated); assert.deepEqual(existing.split('\n'), ['status=existing', `branch=${branch}`, `target=eng/upstream/reviews/${branch.slice('review/upstream/'.length)}`, 'snapshot=reused']);
  assert.equal(JSON.parse(readFileSync(join(snapshot, 'selected-branch-snapshot', 'manifest.json'), 'utf8')).base, base);

  const fakeGh = join(temporary, 'gh-wrapper'); writeFileSync(fakeGh, `#!/usr/bin/env bash
if [[ "$1 $2" == 'pr list' ]]; then exit 0; fi
if [[ "$1 $2" == 'pr create' ]]; then echo 'GitHub Actions is not permitted to create or approve pull requests' >&2; exit 1; fi
exit 2
`, { mode: 0o755 });
  const evidence = join(temporary, 'evidence'); const deferred = run('bash', [openPr, '--repo', 'Runic-Artifex/ReactiveUI.Validation', '--branch', branch, '--base', base, '--upstream', candidate, '--month', '2026-10', '--evidence-dir', evidence], root, { GH_BIN: fakeGh });
  assert.match(deferred, /^status=deferred/); assert.match(readFileSync(join(evidence, 'pr-status.md'), 'utf8'), /compare\/main\.\.\.Runic-Artifex/);
  console.log('review fixtures passed: inherited linear provenance, immutable snapshot branch, isolated identity, existing branch reuse, and deferred Validation PR');
} finally { rmSync(temporary, { recursive: true, force: true }); }
