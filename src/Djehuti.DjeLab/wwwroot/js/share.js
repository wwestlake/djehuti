// Tiny Web Share / clipboard interop for ShareButton.razor. No dependencies.
export function canShare() {
    return typeof navigator !== "undefined" && typeof navigator.share === "function";
}

export async function nativeShare(title, text, url) {
    if (!canShare()) {
        return false;
    }
    try {
        await navigator.share({ title, text, url });
        return true;
    } catch (err) {
        // AbortError just means the visitor dismissed the OS share sheet.
        if (err && err.name === "AbortError") {
            return true;
        }
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

export function currentTitle() {
    return document.title;
}
