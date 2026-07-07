// Entry script for the Web Worker that runs Spinoza programs off the UI
// thread. Boots a second, independent .NET WASM runtime instance inside
// this worker (reusing the same published _framework as the main page, via
// a relative import so this keeps working under any deployment subpath),
// then dispatches 'run' commands from the main thread into the exported
// SpinozaWorker.Run method.
import { dotnet } from '../_framework/dotnet.js';

let assemblyExports;
let startupError;

try {
    const { getAssemblyExports, getConfig } = await dotnet.create();
    const config = getConfig();
    assemblyExports = await getAssemblyExports(config.mainAssemblyName);
    await assemblyExports.Djehuti.DjeLab.Simulation.SpinozaWorker.Init();
    self.postMessage({ command: 'ready' });
} catch (err) {
    startupError = err instanceof Error ? err.message : String(err);
    self.postMessage({ command: 'ready', error: startupError });
}

self.addEventListener('message', (e) => {
    const { command, runId, source } = e.data;
    try {
        if (!assemblyExports) throw new Error(startupError || 'worker runtime failed to start');
        if (command === 'run') {
            assemblyExports.Djehuti.DjeLab.Simulation.SpinozaWorker.Run(runId, source);
        } else {
            throw new Error(`Unknown command: ${command}`);
        }
    } catch (err) {
        self.postMessage({
            command: 'error',
            runId,
            message: err instanceof Error ? err.message : String(err),
        });
    }
});
