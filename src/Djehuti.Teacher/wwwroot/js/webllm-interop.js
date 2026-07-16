// Real in-browser LLM inference via WebGPU (mlc-ai/web-llm), loaded from
// CDN as an ESM module -- no bundler, matches how Plotly/KaTeX/Ace are
// loaded elsewhere in this app. This is NOT a scripted/fake fallback: it
// downloads and runs an actual small model (Qwen2.5-0.5B-Instruct) entirely
// client-side, no server round-trip, no API key. First use downloads the
// model (a few hundred MB) into IndexedDB cache; subsequent loads reuse
// that cache and are fast.
//
// One engine instance is kept module-scope (not per-Blazor-component) so
// switching panes/pages doesn't reload the model.
//
// Requirements: WebGPU support (navigator.gpu) + compatible GPU/driver.
// If download fails (net::ERR_FAILED), check browser console logs and
// consider: (1) network connectivity to Hugging Face CDN, (2) browser
// storage quota, (3) GPU compatibility. Alternatives: use API key in
// AI Setup for a remote tutor, or use a simpler model like
// "Phi-3.5-mini-instruct-q4f16_1-MLC".

let enginePromise = null;
let webllmModule = null;

const MODEL_ID = "Qwen2.5-0.5B-Instruct-q4f16_1-MLC";
const CUSTOM_MODEL_URL = "https://us-east-1-886110331954-us-east-2-an.s3.us-east-2.amazonaws.com/models/qwen-2.5-0.5b/";

export function isSupported() {
    return typeof navigator !== "undefined" && !!navigator.gpu;
}

export async function init(dotNetRef) {
    if (enginePromise) return true;

    if (!isSupported()) {
        console.error("[WebLLM] WebGPU not supported - navigator.gpu is not available");
        return false;
    }

    // Test basic network connectivity to diagnostic URLs
    console.log("[WebLLM] Testing network connectivity...");
    try {
        const testResponses = await Promise.allSettled([
            fetch("https://huggingface.co/", { method: "HEAD", mode: "no-cors" }).then(() => "huggingface.co: OK"),
            fetch("https://cdn-lfs.huggingface.co/", { method: "HEAD", mode: "no-cors" }).then(() => "cdn-lfs: OK"),
        ]);
        testResponses.forEach(r => {
            if (r.status === "fulfilled") console.log(`[WebLLM] Network test: ${r.value}`);
            else console.log(`[WebLLM] Network test failed: ${r.reason}`);
        });
    } catch (e) {
        console.warn("[WebLLM] Network diagnostic failed:", e);
    }

    enginePromise = (async () => {
        try {
            console.log("[WebLLM] Loading web-llm module from esm.run...");
            webllmModule = await import("https://esm.run/@mlc-ai/web-llm");
            console.log("[WebLLM] web-llm module loaded, CreateMLCEngine available:", !!webllmModule.CreateMLCEngine);

            console.log(`[WebLLM] Starting engine init for model: ${MODEL_ID}`);
            console.log(`[WebLLM] Using custom model host: ${CUSTOM_MODEL_URL}`);

            // Use custom model URL from our server instead of Hugging Face CDN
            const engine = await webllmModule.CreateMLCEngine(MODEL_ID, {
                model: CUSTOM_MODEL_URL,
                cacheBackend: "indexeddb",
                initProgressCallback: (report) => {
                    console.log(`[WebLLM] Progress: ${report.text ?? "..."} (${(report.progress * 100).toFixed(1)}%)`);
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync("OnWebLlmProgress", report.text ?? "", report.progress ?? 0);
                    }
                },
            });
            console.log("[WebLLM] Engine initialized successfully");
            return engine;
        } catch (err) {
            console.error("[WebLLM] Engine init failed:", err);
            console.error("[WebLLM] Error details:", {
                name: err.name,
                message: err.message,
                stack: err.stack
            });
            throw err;
        }
    })();

    try {
        await enginePromise;
        return true;
    } catch (err) {
        console.error("[WebLLM] init wrapper failed:", err);
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
