// Main-thread bridge to spinoza-worker.js. Each SpinozaWorkerClient.cs
// instance owns exactly one Worker (one per active graph pane run), so
// multiple graph windows running independent simulations never cross-talk.
//
// workerScriptUrl is computed on the C# side (via NavigationManager) rather
// than resolved here via import.meta.url -- this module is itself loaded
// through Blazor's dynamic import() mechanism, which resolves its own
// import.meta.url to a virtualized path under _framework/, not where it's
// actually served from, so a sibling-relative URL computed from in here
// would point at the wrong place.
export function createWorker(dotNetHelper, workerScriptUrl) {
    const worker = new Worker(workerScriptUrl, { type: 'module' });
    worker.addEventListener('message', (e) => {
        const { command, runId, json, message, error } = e.data;
        switch (command) {
            case 'ready':
                dotNetHelper.invokeMethodAsync('OnWorkerReady', error ?? null);
                break;
            case 'emit':
                dotNetHelper.invokeMethodAsync('OnEmit', runId, json);
                break;
            case 'result':
                dotNetHelper.invokeMethodAsync('OnResult', runId, json);
                break;
            case 'error':
                dotNetHelper.invokeMethodAsync('OnError', runId, message);
                break;
        }
    });
    return worker;
}

export function postRun(worker, runId, source, runtimeDataJson) {
    worker.postMessage({ command: 'run', runId, source, runtimeDataJson: runtimeDataJson ?? null });
}

export function terminate(worker) {
    worker.terminate();
}
