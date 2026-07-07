// postEmit/postResult/postError, imported into the worker's own C# via
// JSHost.ImportAsync (see Simulation/SpinozaWorker.cs's Init method) so a
// running Spinoza program can call back out to postMessage synchronously,
// as each emit(...) call happens -- not batched until Run returns.
//
// Deliberately a SEPARATE file from spinoza-worker.js, not exports living
// alongside that file's own bootstrap code: JSHost.ImportAsync dynamically
// imports its target module, and dynamically importing the SAME module that
// is the worker's own still-executing entry script deadlocks (the import
// can't resolve until the entry script finishes running, which can't
// happen until the import resolves).
export function postEmit(runId, json) {
    self.postMessage({ command: 'emit', runId, json });
}

export function postResult(runId, json) {
    self.postMessage({ command: 'result', runId, json });
}

export function postError(runId, message) {
    self.postMessage({ command: 'error', runId, message });
}
