import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { displayValue, formatMinorUnits, isTerminalStatus, statusDescription } from '../../src/BatchDemo.Api/wwwroot/app.js';

test('polling terminates for every terminal state and not Received', () => {
  for (const status of ['Ready','ReadyWithExceptions','Rejected','ProcessingFailed','Duplicate']) assert.equal(isTerminalStatus(status), true);
  assert.equal(isTerminalStatus('Received'), false);
});
test('terminal result descriptions cover all outcomes', () => {
  for (const status of ['Ready','ReadyWithExceptions','Rejected','ProcessingFailed','Duplicate']) assert.notEqual(statusDescription(status), 'Status is being updated.');
});
test('minor units and nullable rejection identity format for display', () => {
  assert.equal(formatMinorUnits(2495), '24.95'); assert.equal(displayValue(null), '—'); assert.equal(displayValue(undefined), '—');
});
test('rendering uses text nodes and never injects artifact values as HTML', async () => {
  const source = await readFile(new URL('../../src/BatchDemo.Api/wwwroot/app.js', import.meta.url), 'utf8');
  assert.match(source, /textContent/); assert.doesNotMatch(source, /innerHTML|insertAdjacentHTML|document\.write/);
});
