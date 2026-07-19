// Triggers a browser download of text content -- used by the free-tier
// local-project flow (no server round trip, the whole project bundle is
// serialized client-side and handed to the browser as a file).
export function downloadTextFile(filename, content, mimeType) {
    const blob = new Blob([content], { type: mimeType || "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}
