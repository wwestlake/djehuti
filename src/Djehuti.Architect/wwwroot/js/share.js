// Tiny JS interop helpers for the ShareButton component. No third-party code.
export function canShare() {
    return typeof navigator !== "undefined" && typeof navigator.share === "function";
}

export async function nativeShare(title, text, url) {
    try {
        await navigator.share({ title, text, url });
        return true;
    } catch (err) {
        // AbortError just means the user dismissed the OS share sheet.
        if (err && err.name === "AbortError") return false;
        console.error("share failed", err);
        return false;
    }
}

export async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (err) {
        console.error("clipboard write failed", err);
        return false;
    }
}
