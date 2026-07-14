// Real in-browser LLM inference via WebGPU (mlc-ai/web-llm), loaded from
// CDN as an ESM module -- no bundler, matches how Plotly/KaTeX/Ace are
// loaded elsewhere in this app. This is NOT a scripted/fake fallback: it
// downloads and runs an actual small model (Qwen2.5-0.5B-Instruct) entirely
// client-side, no server round-trip, no API key. First use downloads the
// model (a few hundred MB) into the browser's Cache Storage; subsequent
// loads reuse that cache and are fast.
//
// One engine instance is kept module-scope (not per-Blazor-component) so
// switching panes/pages doesn't reload the model.

let enginePromise = null;
let webllmModule = null;

const MODEL_ID = "Qwen2.5-0.5B-Instruct-q4f16_1-MLC";

export function isSupported() {
    return typeof navigator !== "undefined" && !!navigator.gpu;
}

export async function init(dotNetRef) {
    if (enginePromise) return true;

    if (!isSupported()) {
        return false;
    }

    enginePromise = (async () => {
        webllmModule = await import("https://esm.run/@mlc-ai/web-llm");
        const engine = await webllmModule.CreateMLCEngine(MODEL_ID, {
            initProgressCallback: (report) => {
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync("OnWebLlmProgress", report.text ?? "", report.progress ?? 0);
                }
            },
        });
        return engine;
    })();

    try {
        await enginePromise;
        return true;
    } catch (err) {
        console.error("[WebLLM] init failed:", err);
        enginePromise = null;
        return false;
    }
}

// messagesJson: JSON string of [{ role, content }, ...]. Returns the full
// reply text (non-streaming) -- simpler to bridge into Blazor's render
// cycle than token-by-token streaming, at the cost of no partial output
// while it's thinking. Revisit with a streaming JS callback into a
// dotNetRef if that turns out to matter.
export async function chat(messagesJson) {
    if (!enginePromise) throw new Error("WebLLM engine not initialized");
    const engine = await enginePromise;
    const messages = JSON.parse(messagesJson);
    const reply = await engine.chat.completions.create({ messages });
    return reply.choices[0]?.message?.content ?? "";
}
