using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Djehuti.DjeLab.Dsl;
using Microsoft.FSharp.Core;

namespace Djehuti.DjeLab.Simulation;

/// <summary>
/// Runs inside a dedicated Web Worker (see wwwroot/js/spinoza-worker.js),
/// never on the main UI thread -- a long or non-terminating Spinoza program
/// blocks only this worker's own thread, not the page. `emit(...)` calls
/// inside the running program post messages back to the main thread as they
/// happen, via PostEmit, so a graph pane can render points live while the
/// program is still executing rather than waiting for it to finish.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class SpinozaWorker
{
    [JSExport]
    internal static async Task Init()
    {
        // JSHost.ImportAsync resolves a relative moduleUrl against the
        // runtime's _framework/ root, not the site root -- ../ steps back
        // out to wwwroot/js/. This is deliberately a SEPARATE file from
        // spinoza-worker.js (the worker's own entry script): dynamically
        // importing a module that is itself the worker's still-executing
        // entry point deadlocks -- the import can't resolve until the
        // entry script finishes, which can't happen until the import
        // resolves.
        await JSHost.ImportAsync("SpinozaWorkerInterop", "../js/spinoza-worker-interop.js");
    }

    [JSExport]
    internal static void Run(string runId, string source)
    {
        try
        {
            var parseResult = Parser.parse(source);
            if (parseResult.IsError)
            {
                PostError(runId, parseResult.ErrorValue);
                return;
            }

            // F#'s `Value -> unit` parameter is FSharpFunc<Value, Unit>, an
            // abstract class, not a delegate -- a C# lambda can't convert to
            // it directly. FromConverter wraps a real Converter<T,TResult>
            // delegate (whose Invoke must return something; null stands in
            // for Unit's one value, since Unit is a reference type) instead.
            var onEmit = FSharpFunc<Evaluator.Value, Unit>.FromConverter(v =>
            {
                var json = Evaluator.toJson(v);
                // A function value emitted mid-run has no JSON form -- drop
                // that one point silently rather than aborting the whole
                // run; a real type error still surfaces via the final
                // result below if the program's overall return value is
                // itself bad.
                if (json.IsOk) PostEmit(runId, json.ResultValue);
                return null!;
            });
            var evalResult = Evaluator.runWithEmit(onEmit, parseResult.ResultValue);

            if (evalResult.IsError)
            {
                PostError(runId, evalResult.ErrorValue);
                return;
            }

            var resultJson = Evaluator.toJson(evalResult.ResultValue);
            if (resultJson.IsOk) PostResult(runId, resultJson.ResultValue);
            else PostError(runId, resultJson.ErrorValue);
        }
        catch (Exception ex)
        {
            PostError(runId, ex.Message);
        }
    }

    [JSImport("postEmit", "SpinozaWorkerInterop")]
    private static partial void PostEmit(string runId, string json);

    [JSImport("postResult", "SpinozaWorkerInterop")]
    private static partial void PostResult(string runId, string json);

    [JSImport("postError", "SpinozaWorkerInterop")]
    private static partial void PostError(string runId, string message);
}
