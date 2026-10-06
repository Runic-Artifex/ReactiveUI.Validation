#!/usr/bin/env node
import { execFile } from 'node:child_process';
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { promisify } from 'node:util';

const run = promisify(execFile);
const repository = 'reactiveui/ReactiveUI.Validation';
const gh = process.env.GH_BIN ?? 'gh';
const usage = 'Usage: node eng/upstream/collect-inventory.mjs --output <file> --upstream <40-hex-sha>';

function argumentsFor(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    if (!argv[index]?.startsWith('--') || argv[index + 1] === undefined) throw new Error(usage);
    result[argv[index].slice(2)] = argv[index + 1];
  }
  if (!result.output || !/^[0-9a-f]{40}$/i.test(result.upstream ?? '')) throw new Error(usage);
  return result;
}

async function api(endpoint, paginate = false) {
  const args = ['api', ...(paginate ? ['--paginate', '--slurp'] : []), `repos/${repository}${endpoint ? `/${endpoint}` : ''}`];
  const { stdout } = await run(gh, args, { maxBuffer: 40 * 1024 * 1024 });
  const parsed = JSON.parse(stdout);
  return paginate ? parsed.flat() : parsed;
}

function normalize(item, type) {
  return {
    number: item.number,
    type,
    title: item.title,
    url: item.html_url,
    state: item.merged_at ? 'merged' : item.state,
    draft: type === 'pull_request' ? Boolean(item.draft) : undefined,
    createdAt: item.created_at,
    updatedAt: item.updated_at,
    closedAt: item.closed_at ?? undefined,
    mergedAt: item.merged_at ?? undefined,
    labels: item.labels.map(label => label.name),
    headCommit: item.head?.sha,
    baseBranch: item.base?.ref,
    mergeCommit: item.merge_commit_sha,
    assessment: {
      status: 'unassessed-for-this-review',
      note: 'Assess this item against this month\'s pinned Runic base and upstream candidate. Dated research is not copied forward as a current decision.',
    },
  };
}

const options = argumentsFor(process.argv.slice(2));
const [issueRecords, pulls, metadata, upstream] = await Promise.all([
  api('issues?state=all&per_page=100&sort=created&direction=asc', true),
  api('pulls?state=all&per_page=100&sort=created&direction=asc', true),
  api(''),
  api('commits/main'),
]);
const issues = issueRecords.filter(item => !item.pull_request);
const items = [...issues.map(item => normalize(item, 'issue')), ...pulls.map(item => normalize(item, 'pull_request'))]
  .sort((left, right) => left.number - right.number);
const open = items.filter(item => item.state === 'open').length;
if (open !== metadata.open_issues_count) {
  throw new Error(`Open item count changed while collecting: ${open} versus ${metadata.open_issues_count}. Retry the collection.`);
}
const snapshot = {
  schemaVersion: 1,
  collectedAt: new Date().toISOString(),
  repository,
  requestedUpstreamCommit: options.upstream.toLowerCase(),
  observedUpstreamMainCommit: upstream.sha,
  coverage: 'Paginated REST issue and pull-request records. Discussions, deleted/inaccessible objects, review threads and CI runs are excluded.',
  assessmentPolicy: 'Every item is unassessed for this monthly review. Historical research is dated evidence and is not reused as a current disposition.',
  counts: { issues: issues.length, pullRequests: pulls.length, open, total: items.length },
  items,
};
mkdirSync(dirname(resolve(options.output)), { recursive: true });
writeFileSync(options.output, `${JSON.stringify(snapshot, null, 2)}\n`, { flag: 'wx' });
console.log(JSON.stringify({ output: resolve(options.output), ...snapshot.counts, observedUpstreamMainCommit: upstream.sha }, null, 2));
