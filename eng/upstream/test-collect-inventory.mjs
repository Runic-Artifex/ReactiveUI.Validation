#!/usr/bin/env node
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

const root = resolve(import.meta.dirname, '../..');
const collector = resolve(root, 'eng/upstream/collect-inventory.mjs');
const temporary = mkdtempSync(join(tmpdir(), 'runic-validation-upstream-collector-'));
const pin = '0123456789abcdef0123456789abcdef01234567';
function call(args, env) { return execFileSync('node', [collector, ...args], { cwd: root, env: { ...process.env, ...env }, encoding: 'utf8', stdio: 'pipe' }).trim(); }
function mock(name, issueMode) {
  const file = join(temporary, name);
  writeFileSync(file, `#!/usr/bin/env node
const endpoint = process.argv.find(x => x.startsWith('repos/reactiveui/ReactiveUI.Validation'));
const pin = '${pin}';
const issues = Array.from({length: 2}, (_, i) => ({number:i + 1,title:'issue',html_url:'https://example.invalid/i',state:'open',created_at:'2026-01-01T00:00:00Z',updated_at:'2026-01-01T00:00:00Z',closed_at:null,labels:[]}));
const pulls = Array.from({length: 3}, (_, i) => ({number:i + 10,title:'pull',html_url:'https://example.invalid/p',state:'open',created_at:'2026-01-01T00:00:00Z',updated_at:'2026-01-01T00:00:00Z',closed_at:null,labels:[],merged_at:null,draft:false,head:{sha:pin},base:{ref:'main'},merge_commit_sha:null}));
if(endpoint.includes('/issues?')) console.log(JSON.stringify([${issueMode ? 'pulls.map(p => ({...p,pull_request:{}}))' : 'issues'}]));
else if(endpoint.includes('/pulls?')) console.log(JSON.stringify([pulls]));
else if(endpoint.endsWith('/commits/main')) console.log(JSON.stringify({sha:pin}));
else if(endpoint === 'repos/reactiveui/ReactiveUI.Validation') console.log(JSON.stringify({open_issues_count:5}));
else process.exit(2);
`, { mode: 0o755 });
  return file;
}
try {
  const good = join(temporary, 'good.json');
  call(['--output', good, '--upstream', pin], { GH_BIN: mock('good-gh', false) });
  const snapshot = JSON.parse(readFileSync(good, 'utf8'));
  assert.equal(snapshot.repository, 'reactiveui/ReactiveUI.Validation');
  assert.equal(snapshot.counts.open, 5);
  assert.ok(snapshot.items.every(item => item.assessment.status === 'unassessed-for-this-review'));
  let error;
  try { call(['--output', join(temporary, 'bad.json'), '--upstream', pin], { GH_BIN: mock('pr-only-gh', true) }); } catch (failure) { error = failure; }
  assert.ok(error); assert.match(String(error.stderr), /3 versus 5/); assert.equal(existsSync(join(temporary, 'bad.json')), false);
  console.log('collector fixtures passed: complete Validation inventory and PR-only response rejection');
} finally { rmSync(temporary, { recursive: true, force: true }); }
