export const TERMINAL_STATUSES = new Set(["Ready","ReadyWithExceptions","Rejected","ProcessingFailed","Duplicate"]);
export const isTerminalStatus = status => TERMINAL_STATUSES.has(status);
export const formatMinorUnits = value => (Number(value) / 100).toFixed(2);
export const displayValue = value => value === null || value === undefined || value === "" ? "—" : String(value);
export const isValidBatchId = value => typeof value === "string" && /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(value);
export const batchIdFromUrl = url => new URL(url).searchParams.get("batchId");
export function updateBatchUrl(batchId, browserHistory, currentUrl, method="pushState") {
  const url=new URL(currentUrl);if(batchId)url.searchParams.set("batchId",batchId);else url.searchParams.delete("batchId");browserHistory[method]({},"",`${url.pathname}${url.search}${url.hash}`);
}
export const statusDescription = status => ({
  Ready:"All submitted instructions were accepted.",
  ReadyWithExceptions:"Some instructions were accepted and some require correction.",
  Rejected:"No instructions can proceed.",
  ProcessingFailed:"The system could not complete processing.",
  Duplicate:"This file was previously received.",
  Received:"The batch is queued or processing."
}[status] || "Status is being updated.");

const $ = id => document.getElementById(id);
const text = (tag, value, className) => { const node=document.createElement(tag); node.textContent=displayValue(value); if(className)node.className=className; return node; };
const setHidden = (node, hidden) => node.classList.toggle("hidden", hidden);
const addDetails = (root, entries) => { root.replaceChildren(); for(const [label,value] of entries){const box=document.createElement("div");box.append(text("dt",label),text("dd",value));root.append(box);} };
const problemText = async response => { try { const body=await response.json(); return body.detail || body.title || Object.values(body.errors||{}).flat().join(" ") || `Request failed (${response.status}).`; } catch { return `Request failed (${response.status}).`; } };

export function renderRejectedRows(root, records){
  root.replaceChildren();
  for(const record of records) for(const reason of record.reasons){
    const row=document.createElement("tr");
    row.append(text("td",record.sourceRowNumber),text("td",record.merchantReference),text("td",reason.code),text("td",reason.message),text("td",reason.field),text("td",record.originalRowContent,"row-content"));root.append(row);
  }
}

function renderAcceptedRows(root, records){root.replaceChildren();for(const record of records){const row=document.createElement("tr");row.append(text("td",record.sourceRowNumber),text("td",record.merchantReference),text("td",record.operation),text("td",formatMinorUnits(record.amountMinor)),text("td",record.currency),text("td",record.requestedExecutionDate),text("td",record.originalAuthorizationReference));root.append(row);}}
function showBatch(batch){setHidden($("batch-panel"),false);$("status-badge").textContent=batch.status;$("status-badge").className=`status ${batch.status.toLowerCase()}`;$("status-description").textContent=statusDescription(batch.status);addDetails($("batch-details"),[["Batch ID",batch.batchId],["Merchant ID",batch.merchantId],["Original filename",batch.originalFilename],["Received",new Date(batch.receivedAt).toLocaleString()]]);}
function metric(label,value){const node=document.createElement("div");node.className="metric";node.append(text("strong",value),text("span",label));return node;}
function showResult(result){showBatch(result);setHidden($("result-panel"),false);$("metrics").replaceChildren(metric("Total rows",result.totalRows ?? 0),metric("Accepted",result.acceptedRows ?? 0),metric("Rejected",result.rejectedRows ?? 0),metric("Final status",result.status));renderAcceptedRows($("accepted-body"),result.accepted);renderRejectedRows($("rejected-body"),result.rejected);setHidden($("accepted-empty"),result.accepted.length>0);setHidden($("rejected-empty"),result.rejected.length>0);const a=result.artifacts;addDetails($("summary-details"),[["Original filename",result.originalFilename],["Original SHA-256",result.originalSha256],["Ingested",result.ingestedAt ? new Date(result.ingestedAt).toLocaleString():null],["Artifact generated",result.artifactGeneratedAt ? new Date(result.artifactGeneratedAt).toLocaleString():null],["Original artifact",a?.original],["Accepted artifact",a?.accepted],["Rejected artifact",a?.rejected],["Summary artifact",a?.summary],["Canonical batch ID",result.canonicalBatchId]]);const reasons=$("file-reasons");reasons.replaceChildren();if(result.fileRejectionReasons.length){reasons.className="reason-list";reasons.append(text("strong","File-level rejection reasons"));const list=document.createElement("ul");for(const reason of result.fileRejectionReasons)list.append(text("li",`${reason.code}: ${reason.message}`));reasons.append(list);}else reasons.className="";}
async function getJson(url,signal){const response=await fetch(url,{headers:{Accept:"application/json"},signal});if(!response.ok)throw new Error(await problemText(response));return response.json();}
const waitFor=(milliseconds,signal)=>new Promise((resolve,reject)=>{const timer=setTimeout(resolve,milliseconds);signal.addEventListener("abort",()=>{clearTimeout(timer);reject(new DOMException("Cancelled","AbortError"));},{once:true});});
export function createBatchLoader({request,wait=waitFor,onBatch,onResult,onError}){
  let generation=0;let controller=null;
  const cancel=()=>{generation++;controller?.abort();controller=null;};
  const load=async batchId=>{cancel();const mine=generation;controller=new AbortController();let failures=0;while(mine===generation){try{const batch=await request(`/api/batches/${encodeURIComponent(batchId)}`,controller.signal);if(mine!==generation)return;onBatch(batch);failures=0;if(isTerminalStatus(batch.status)){const result=await request(`/api/batches/${encodeURIComponent(batch.batchId)}/results`,controller.signal);if(mine!==generation)return;onResult(result);return;}await wait(1500,controller.signal);}catch(error){if(mine!==generation||error?.name==="AbortError")return;if(++failures>=3){onError(new Error(`Unable to refresh batch status after ${failures} attempts. ${error.message}`));return;}try{await wait(1500,controller.signal);}catch(waitError){if(waitError?.name==="AbortError")return;}}}};
  return {load,cancel};
}
function showError(error){const node=$("message");node.textContent=error instanceof Error?error.message:String(error);setHidden(node,false);}
function clearView(generateMerchant=true){setHidden($("result-panel"),true);setHidden($("batch-panel"),true);setHidden($("message"),true);setHidden($("reset"),true);$("accepted-body").replaceChildren();$("rejected-body").replaceChildren();if(generateMerchant){$("upload-form").reset();$("merchant-id").value=`demo-${new Date().toISOString().slice(0,10).replaceAll("-","")}-${crypto.randomUUID().slice(0,8)}`;}}
function addCanonicalLink(result){if(result.status!=="Duplicate"||!result.canonicalBatchId)return;const link=document.createElement("a");link.href=`/?batchId=${result.canonicalBatchId}`;link.textContent="View canonical batch result";link.addEventListener("click",event=>{event.preventDefault();updateBatchUrl(result.canonicalBatchId,history,location.href);restoreFromUrl();});$("status-description").append(" ",link);}
let loader;
function restoreFromUrl(){loader.cancel();clearView(false);const batchId=batchIdFromUrl(location.href);if(!batchId)return;if(!isValidBatchId(batchId)){showError(new Error("The batchId in the URL is not a valid GUID."));return;}setHidden($("reset"),false);loader.load(batchId);}
function resetPortal(){loader.cancel();updateBatchUrl(null,history,location.href);clearView(true);$("merchant-id").focus();}
function init(){loader=createBatchLoader({request:getJson,onBatch:batch=>{setHidden($("reset"),false);showBatch(batch);},onResult:result=>{showResult(result);addCanonicalLink(result);},onError:showError});clearView(true);$("upload-form").addEventListener("submit",async event=>{event.preventDefault();loader.cancel();const button=$("upload-button");setHidden($("message"),true);button.disabled=true;button.textContent="Uploading…";let batch;try{const file=$("file").files[0];if(!file||!file.name.toLowerCase().endsWith(".csv"))throw new Error("Select a CSV file to upload.");const response=await fetch("/api/batches",{method:"POST",body:new FormData(event.currentTarget)});if(!response.ok)throw new Error(await problemText(response));batch=await response.json();}catch(error){showError(error);}finally{button.disabled=false;button.textContent="Upload and process";}if(batch){updateBatchUrl(batch.batchId,history,location.href);restoreFromUrl();}});$("reset").addEventListener("click",resetPortal);window.addEventListener("popstate",restoreFromUrl);restoreFromUrl();}
if(typeof document!=="undefined")init();
