// The GitHub Project that holds ticket status. Needs a token that can write Projects
// (the factory app's token; the default workflow token cannot).
//   FACTORY_PROJECT_OWNER, FACTORY_PROJECT_NUMBER identify the project (set by setup-github.sh).
import { run } from './lib.mjs';

export const STATUSES = ['Backlog', 'Ready', 'In progress', 'In review', 'Done'];

function ghJson(args) {
  return JSON.parse(run('gh', [...args, '--format', 'json']));
}

function project() {
  const owner = process.env.FACTORY_PROJECT_OWNER;
  const number = process.env.FACTORY_PROJECT_NUMBER;
  if (!owner || !number) throw new Error('FACTORY_PROJECT_OWNER and FACTORY_PROJECT_NUMBER must be set');
  return { owner, number };
}

let cached = null;
function meta() {
  if (cached) return cached;
  const { owner, number } = project();
  const view = ghJson(['project', 'view', number, '--owner', owner]);
  const fields = ghJson(['project', 'field-list', number, '--owner', owner]).fields;
  const status = fields.find((f) => f.name === 'Status');
  if (!status) throw new Error('the project has no Status field');
  cached = { owner, number, projectId: view.id, fieldId: status.id, options: Object.fromEntries(status.options.map((o) => [o.name, o.id])) };
  return cached;
}

function sleep(ms) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

/**
 * Adds an issue (by URL) to the project; returns the item id. Adding twice returns the same item,
 * but when the project's own workflow adds the issue at the same moment, item-add fails with
 * "Content already exists in this project". The item is then looked up in the list, which may not
 * show it at once: three reads, two seconds apart, before the error is thrown again.
 * `gh` and `wait` are parameters so that a test passes fakes.
 */
export function addItem(issueUrl, { gh = ghJson, wait = sleep } = {}) {
  const { owner, number } = project();
  try {
    return gh(['project', 'item-add', number, '--owner', owner, '--url', issueUrl]).id;
  } catch (e) {
    if (!String(e.message).includes('already exists in this project')) throw e;
    for (let read = 1; read <= 3; read++) {
      if (read > 1) wait(2000);
      const items = gh(['project', 'item-list', number, '--owner', owner, '--limit', '2000']).items;
      const item = items.find((i) => i.content?.url === issueUrl);
      if (item) return item.id;
    }
    throw e;
  }
}

export function setStatus(issueUrl, status) {
  if (!STATUSES.includes(status)) throw new Error(`unknown status "${status}"; use one of: ${STATUSES.join(', ')}`);
  const m = meta();
  const option = m.options[status];
  if (!option) throw new Error(`the project's Status field has no option "${status}"`);
  const itemId = addItem(issueUrl);
  run('gh', ['project', 'item-edit', '--id', itemId, '--project-id', m.projectId, '--field-id', m.fieldId, '--single-select-option-id', option]);
  console.log(`${issueUrl} → ${status}`);
}

/** Status name per issue number, for the whole project. */
export function statuses() {
  const { owner, number } = project();
  const items = ghJson(['project', 'item-list', number, '--owner', owner, '--limit', '2000']).items;
  return new Map(items.filter((i) => i.content?.number).map((i) => [i.content.number, i.status ?? null]));
}
