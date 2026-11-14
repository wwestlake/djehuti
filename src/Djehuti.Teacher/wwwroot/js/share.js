// Tiny JS interop for the ShareButton component. No third-party code --
// just the native Web Share / Clipboard APIs, feature-detected.

export function canShare() {
    return typeof navigator !== "undefined" && !!navigator.share;
}

export async function nativeShare(title, text, url) {
    try {
        await navigator.share({ title, text, url });
        return true;
    } catch (err) {
        // AbortError: the user dismissed the native share sheet -- not a failure.
        if (err && err.name === "AbortError") return true;
        return false;
    }
}

export async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}
