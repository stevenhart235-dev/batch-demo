export const TERMINAL_STATUSES = new Set(["Ready","ReadyWithExceptions","Rejected","ProcessingFailed","Duplicate"]);
export const isTerminalStatus = status => TERMINAL_STATUSES.has(status);
export const formatMinorUnits = value => (Number(value) / 100).toFixed(2);
export const displayValue = value => value === null || value === undefined || value === "" ? "—" : String(value);
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
async function getJson(url){const response=await fetch(url,{headers:{Accept:"application/json"}});if(!response.ok)throw new Error(await problemText(response));return response.json();}
async function poll(statusUrl){let failures=0;for(;;){try{const batch=await getJson(statusUrl);showBatch(batch);failures=0;if(isTerminalStatus(batch.status)){const result=await getJson(`/api/batches/${batch.batchId}/results`);showResult(result);if(result.status==="Duplicate"&&result.canonicalBatchId){const link=document.createElement("a");link.href=`/?batchId=${result.canonicalBatchId}`;link.textContent="View canonical batch result";$("status-description").append(" ",link);}return;}await new Promise(resolve=>setTimeout(resolve,1500));}catch(error){if(++failures>=3)throw new Error(`Unable to refresh batch status after ${failures} attempts. ${error.message}`);await new Promise(resolve=>setTimeout(resolve,1500));}}}
function showError(error){const node=$("message");node.textContent=error instanceof Error?error.message:String(error);setHidden(node,false);}
function reset(){setHidden($("result-panel"),true);setHidden($("batch-panel"),true);setHidden($("message"),true);setHidden($("reset"),true);$("upload-form").reset();$("merchant-id").value=`demo-${new Date().toISOString().slice(0,10).replaceAll("-","")}-${crypto.randomUUID().slice(0,8)}`;$("merchant-id").focus();}
async function init(){reset();const batchId=new URLSearchParams(location.search).get("batchId");if(batchId){setHidden($("reset"),false);try{await poll(`/api/batches/${encodeURIComponent(batchId)}`);}catch(error){showError(error);}return;}$("upload-form").addEventListener("submit",async event=>{event.preventDefault();const button=$("upload-button");setHidden($("message"),true);button.disabled=true;button.textContent="Uploading…";try{const file=$("file").files[0];if(!file||!file.name.toLowerCase().endsWith(".csv"))throw new Error("Select a CSV file to upload.");const response=await fetch("/api/batches",{method:"POST",body:new FormData(event.currentTarget)});if(!response.ok)throw new Error(await problemText(response));const batch=await response.json();setHidden($("reset"),false);showBatch(batch);await poll(batch.statusUrl);}catch(error){showError(error);}finally{button.disabled=false;button.textContent="Upload and process";}});$("reset").addEventListener("click",reset);}
if(typeof document!=="undefined")init();
