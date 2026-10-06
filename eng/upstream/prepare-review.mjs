#!/usr/bin/env node
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';

const usage = 'Usage: node eng/upstream/prepare-review.mjs --repo <repo> --base <sha> --upstream <sha> --month YYYY-MM --inventory <file> --output <new-directory>';
function options(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    if (!argv[index]?.startsWith('--') || argv[index + 1] === undefined) throw new Error(usage);
    result[argv[index].slice(2)] = argv[index + 1];
  }
  if (!result.repo || !result.inventory || !result.output || !/^\d{4}-(0[1-9]|1[0-2])$/.test(result.month ?? '') ||
      !/^[0-9a-f]{40}$/i.test(result.base ?? '') || !/^[0-9a-f]{40}$/i.test(result.upstream ?? '')) throw new Error(usage);
  return result;
}
function git(repo, args) {
  return execFileSync('git', ['-C', repo, ...args], { encoding: 'utf8', maxBuffer: 20 * 1024 * 1024 }).trim();
}
function outcome(repo, args) {
  try { return { ok: true, status: 0, output: git(repo, args) }; }
  catch (error) { return { ok: false, status: Number.isInteger(error.status) ? error.status : null, output: String(error.stdout ?? error.stderr ?? '').trim() }; }
}
function commit(repo, sha, label) {
  if (git(repo, ['rev-parse', `${sha}^{commit}`]).toLowerCase() !== sha.toLowerCase()) throw new Error(`${label} is not an exact commit: ${sha}`);
}
function upstreamProvenance(repo, base) {
  const upstreamMain = outcome(repo, ['rev-parse', '--verify', 'refs/remotes/upstream/main^{commit}']);
  if (!upstreamMain.ok) return { status: 'not-found', reason: 'No fetched upstream/main reference.' };
  const candidates = [];
  for (const line of git(repo, ['rev-list', '--merges', '--parents', base]).split('\n').filter(Boolean)) {
    const [merge, firstParent, ...others] = line.split(' ');
    if (outcome(repo, ['merge-base', '--is-ancestor', firstParent, upstreamMain.output]).ok) continue;
    const imported = others.filter(parent => outcome(repo, ['merge-base', '--is-ancestor', parent, upstreamMain.output]).ok);
    if (imported.length === 1) candidates.push({ merge, runicParent: firstParent, upstreamParent: imported[0] });
    if (imported.length > 1) return { status: 'ambiguous-real-merge', candidates: [...candidates, { merge, runicParent: firstParent, upstreamParents: imported }] };
  }
  const maximal = candidates.filter(candidate => !candidates.some(other => other !== candidate && outcome(repo, ['merge-base', '--is-ancestor', candidate.upstreamParent, other.upstreamParent]).ok));
  if (maximal.length === 1) return { status: 'real-merge', ...maximal[0] };
  if (maximal.length > 1) return { status: 'ambiguous-real-merge', candidates: maximal };
  const shared = git(repo, ['merge-base', base, upstreamMain.output]);
  return { status: 'inherited-shared-merge-base', commit: shared, reason: 'No real upstream merge was found. This shared ancestor is verifiable inherited ancestry, not an asserted import merge.' };
}
function provenanceText(provenance) {
  if (provenance.status === 'real-merge') return `Real merge: \`${provenance.upstreamParent}\` via \`${provenance.merge}\`.`;
  if (provenance.status === 'inherited-shared-merge-base') return `Inherited/shared merge base: \`${provenance.commit}\`. ${provenance.reason}`;
  return `${provenance.status}: inspect manifest.json before integration.`;
}
function report(manifest) {
  return `# ${manifest.month} Validation upstream review preparation

This is a pinned preparation record, not a merge approval or implementation review.
It uses Git object IDs and a fresh unassessed inventory; it never executes upstream
workflows, build scripts or project code.

| Input | SHA / result |
| --- | --- |
| Runic base | \`${manifest.base}\` |
| Upstream candidate | \`${manifest.upstream}\` |
| Previous upstream provenance | ${provenanceText(manifest.previousUpstreamProvenance)} |
| Virtual merge | ${manifest.merge.status} |

The inventory has ${manifest.inventory.counts.total} records (${manifest.inventory.counts.open} open). Every record is marked \`unassessed-for-this-review\`; dated investigation text was not reused as a current disposition.

${manifest.commits.count === 0 ? 'The candidate adds no commits beyond the shared merge base; record this no-change review after inspecting the inventory.' : `${manifest.commits.count} candidate commits require review.`}

${manifest.merge.status === 'conflicts' ? 'The virtual merge reports conflicts. No merge was attempted; resolve only on a reviewed temporary sync branch.' : 'The virtual merge is textually clean. Inspect cleanly merged source, dependency, test and workflow changes before any real merge.'}

Do not merge this preparation branch, publish packages/tags, or contact upstream. A later reviewed sync must record decisions and validation in the repository maintenance record.
`;
}

const input = options(process.argv.slice(2));
const repo = resolve(input.repo);
const destination = resolve(input.output);
const inventoryPath = resolve(input.inventory);
if (!existsSync(repo) || !existsSync(inventoryPath)) throw new Error('Repository or inventory is missing.');
if (existsSync(destination)) throw new Error(`Refusing to overwrite existing review snapshot: ${destination}`);
commit(repo, input.base, 'Base');
commit(repo, input.upstream, 'Upstream candidate');
const inventory = JSON.parse(readFileSync(inventoryPath, 'utf8'));
if (inventory.requestedUpstreamCommit?.toLowerCase() !== input.upstream.toLowerCase()) throw new Error('Inventory pin does not match upstream candidate.');
const mergeBase = git(repo, ['merge-base', input.base, input.upstream]);
const virtual = outcome(repo, ['merge-tree', '--write-tree', input.base, input.upstream]);
if (!virtual.ok && virtual.status !== 1) throw new Error(`git merge-tree failed with exit ${virtual.status ?? 'unknown'}: ${virtual.output}`);
const commits = git(repo, ['log', '--format=%H%x09%s', `${mergeBase}..${input.upstream}`]).split('\n').filter(Boolean).map(line => {
  const [sha, subject] = line.split('\t'); return { sha, subject };
});
const manifest = {
  schemaVersion: 1, generatedAt: new Date().toISOString(), month: input.month,
  base: input.base.toLowerCase(), upstream: input.upstream.toLowerCase(), mergeBase,
  previousUpstreamProvenance: upstreamProvenance(repo, input.base),
  merge: { status: virtual.ok ? 'clean' : 'conflicts', virtualTree: virtual.ok ? virtual.output : null, diagnostic: virtual.ok ? null : virtual.output, method: 'git merge-tree --write-tree; no checkout or working-tree merge was performed' },
  commits: { count: commits.length, items: commits },
  changedPaths: git(repo, ['diff', '--name-only', `${mergeBase}..${input.upstream}`]).split('\n').filter(Boolean),
  inventory: { file: 'inventory.json', collectedAt: inventory.collectedAt, observedUpstreamMainCommit: inventory.observedUpstreamMainCommit, counts: inventory.counts, assessmentPolicy: inventory.assessmentPolicy },
};
mkdirSync(destination, { recursive: false });
cpSync(inventoryPath, resolve(destination, 'inventory.json'));
writeFileSync(resolve(destination, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`, { flag: 'wx' });
writeFileSync(resolve(destination, 'report.md'), report(manifest), { flag: 'wx' });
console.log(JSON.stringify({ output: destination, base: manifest.base, upstream: manifest.upstream, merge: manifest.merge.status, commits: manifest.commits.count, provenance: manifest.previousUpstreamProvenance.status, inventory: manifest.inventory.counts }, null, 2));
