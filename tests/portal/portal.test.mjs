import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { batchIdFromUrl, createBatchLoader, displayValue, formatMinorUnits, isTerminalStatus, isValidBatchId, statusDescription, updateBatchUrl } from '../../src/BatchDemo.Api/wwwroot/app.js';

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

test('URL helpers validate, restore, set, and clear batch state', () => {
  const id='6b73b3aa-3e6e-4bd3-90fd-538e022bc43a';const calls=[];const history={pushState:(...args)=>calls.push(args)};
  assert.equal(isValidBatchId(id),true);assert.equal(isValidBatchId('not-a-guid'),false);
  updateBatchUrl(id,history,'http://localhost/?keep=yes');assert.equal(calls[0][2],`/?keep=yes&batchId=${id}`);
  assert.equal(batchIdFromUrl(`http://localhost/${calls[0][2]}`),id);
  updateBatchUrl(null,history,`http://localhost/?keep=yes&batchId=${id}`);assert.equal(calls[1][2],'/?keep=yes');
});

test('active restoration resumes polling and terminal restoration loads results', async () => {
  const id='6b73b3aa-3e6e-4bd3-90fd-538e022bc43a';let statusCalls=0;const batches=[];const results=[];
  const loader=createBatchLoader({request:async url=>url.endsWith('/results')?{status:'Ready',batchId:id}:{status:++statusCalls===1?'Received':'Ready',batchId:id},wait:async()=>{},onBatch:value=>batches.push(value),onResult:value=>results.push(value),onError:assert.fail});
  await loader.load(id);assert.deepEqual(batches.map(x=>x.status),['Received','Ready']);assert.equal(results[0].status,'Ready');
  batches.length=0;results.length=0;statusCalls=5;await loader.load(id);assert.deepEqual(batches.map(x=>x.status),['Ready']);assert.equal(results.length,1);
});

test('cancellation prevents stale polling from replacing newer navigation state', async () => {
  const first='6b73b3aa-3e6e-4bd3-90fd-538e022bc43a';const second='d9428888-122b-4b6c-b78a-e59c70642a18';let releaseFirst;const rendered=[];
  const request=url=>url.includes(first)?new Promise(resolve=>{releaseFirst=resolve}):Promise.resolve(url.endsWith('/results')?{status:'Ready',batchId:second}:{status:'Ready',batchId:second});
  const loader=createBatchLoader({request,wait:async()=>{},onBatch:value=>rendered.push(value.batchId),onResult:value=>rendered.push(value.batchId),onError:assert.fail});
  const stale=loader.load(first);await Promise.resolve();await loader.load(second);releaseFirst({status:'Ready',batchId:first});await stale;
  assert.deepEqual(rendered,[second,second]);
});

test('cancel stops an active polling wait', async () => {
  let waited=false;let rendered=0;const loader=createBatchLoader({request:async()=>({status:'Received'}),wait:(_,signal)=>new Promise(resolve=>{waited=true;signal.addEventListener('abort',resolve,{once:true});}),onBatch:()=>rendered++,onResult:assert.fail,onError:assert.fail});
  const active=loader.load('6b73b3aa-3e6e-4bd3-90fd-538e022bc43a');while(!waited)await Promise.resolve();loader.cancel();await active;assert.equal(rendered,1);
});

test('upload button restoration and popstate wiring are present', async () => {
  const source=await readFile(new URL('../../src/BatchDemo.Api/wwwroot/app.js',import.meta.url),'utf8');
  assert.match(source,/finally\{button\.disabled=false;button\.textContent="Upload and process";/);
  assert.match(source,/addEventListener\("popstate",restoreFromUrl\)/);
});
