const defaultTheme = "ace/theme/tomorrow_night";
const spinozaModeId = "ace/mode/spinoza";

function ensureSpinozaMode() {
    if (!window.ace || (ace.defined && ace.defined[spinozaModeId])) {
        return;
    }

    ace.define("ace/mode/spinoza_highlight_rules", ["require", "exports", "module", "ace/lib/oop", "ace/mode/text_highlight_rules"], function (require, exports, module) {
        const oop = require("ace/lib/oop");
        const TextHighlightRules = require("ace/mode/text_highlight_rules").TextHighlightRules;

        const SpinozaHighlightRules = function () {
            this.$rules = {
                start: [
                    { token: "comment", regex: "#.*$" },
                    { token: "comment", regex: "//.*$" },
                    { token: "string", regex: '"(?:[^"\\\\]|\\\\.)*"' },
                    { token: "string", regex: "'(?:[^'\\\\]|\\\\.)*'" },
                    { token: "constant.numeric", regex: "\\b(?:\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)\\b" },
                    { token: "keyword", regex: "\\b(?:let|rec|in|fun|if|then|else|match|with|type|module|open|import|include|emit|when|for|while|do|done|try|catch|finally|raise|return)\\b" },
                    { token: "support.function", regex: "\\b(?:sqrt|sin|cos|tan|exp|log|abs|min|max|len|map|filter|fold|sum|avg|mean|rand|randint|choice|push|pop|head|tail|zip|range|read|write|load|save)\\b" },
                    { token: "constant.language", regex: "\\b(?:true|false|null|none|unit)\\b" },
                    { token: "keyword.operator", regex: "\\+|\\-|\\*|/|=|<>|<|>|<=|>=|::|->|\\|\\||&&|:=|\\.|,|;|:" },
                    { token: "paren.lparen", regex: "[\\[\\(\\{]" },
                    { token: "paren.rparen", regex: "[\\]\\)\\}]" },
                    { token: "identifier", regex: "[a-zA-Z_][a-zA-Z0-9_']*" },
                    { token: "text", regex: "\\s+" }
                ]
            };
        };

        oop.inherits(SpinozaHighlightRules, TextHighlightRules);
        exports.SpinozaHighlightRules = SpinozaHighlightRules;
    });

    ace.define("ace/mode/spinoza", ["require", "exports", "module", "ace/lib/oop", "ace/mode/text", "ace/mode/spinoza_highlight_rules"], function (require, exports, module) {
        const oop = require("ace/lib/oop");
        const TextMode = require("ace/mode/text").Mode;
        const SpinozaHighlightRules = require("ace/mode/spinoza_highlight_rules").SpinozaHighlightRules;

        const Mode = function () {
            this.HighlightRules = SpinozaHighlightRules;
        };

        oop.inherits(Mode, TextMode);

        (function () {
            this.lineCommentStart = "#";
            this.$id = spinozaModeId;
        }).call(Mode.prototype);

        exports.Mode = Mode;
    });
}

function inferMode(path, contentType) {
    const normalizedPath = (path || "").toLowerCase();
    const ext = normalizedPath.includes(".") ? normalizedPath.slice(normalizedPath.lastIndexOf(".")) : "";
    const type = (contentType || "").toLowerCase();

    if (ext === ".spi" || ext === ".spz" || ext === ".djl" || ext === ".spinoza" || type.includes("spinoza"))
        return "spinoza";
    if (ext === ".cs" || type.includes("csharp"))
        return "csharp";
    if (ext === ".fs" || ext === ".fsx" || type.includes("fsharp"))
        return "fsharp";
    if (ext === ".ts")
        return "typescript";
    if (ext === ".js" || ext === ".mjs" || ext === ".cjs")
        return "javascript";
    if (ext === ".json" || ext === ".jsonl")
        return "json";
    if (ext === ".html" || ext === ".htm")
        return "html";
    if (ext === ".css")
        return "css";
    if (ext === ".md" || ext === ".markdown")
        return "markdown";
    if (ext === ".py")
        return "python";
    if (ext === ".sql")
        return "sql";
    if (ext === ".xml" || ext === ".xaml")
        return "xml";
    if (ext === ".sh" || ext === ".bash")
        return "sh";
    return "text";
}

export function createEditor(hostElement, dotNetRef, initialValue, path, contentType) {
    ensureSpinozaMode();

    const editor = ace.edit(hostElement);
    editor.setTheme(defaultTheme);
    editor.setOptions({
        fontFamily: 'ui-monospace, "Cascadia Code", "SFMono-Regular", Consolas, monospace',
        fontSize: "13px",
        useSoftTabs: true,
        tabSize: 4,
        showGutter: true,
        showPrintMargin: false,
        highlightActiveLine: true,
        wrap: true,
        enableBasicAutocompletion: true,
        enableLiveAutocompletion: true,
        enableSnippets: true,
        scrollPastEnd: 0.25
    });

    const session = editor.session;
    session.setMode(`ace/mode/${inferMode(path, contentType)}`);
    session.setValue(initialValue ?? "", -1);

    let suppressChange = false;
    let changeTimer = null;

    const notifyChange = () => {
        if (suppressChange) {
            suppressChange = false;
            return;
        }

        if (changeTimer) {
            clearTimeout(changeTimer);
        }

        changeTimer = setTimeout(() => {
            changeTimer = null;
            dotNetRef.invokeMethodAsync("OnEditorContentChanged", session.getValue());
        }, 150);
    };

    const notifyCursor = () => {
        const cursor = editor.getCursorPosition();
        dotNetRef.invokeMethodAsync("OnEditorCursorChanged", cursor.row + 1, cursor.column + 1);
    };

    session.on("change", notifyChange);
    editor.selection.on("changeCursor", notifyCursor);
    editor.on("focus", notifyCursor);

    return {
        setValue(value, row, column) {
            suppressChange = true;
            session.setValue(value ?? "", -1);
            if (typeof row === "number" && typeof column === "number") {
                editor.moveCursorTo(row, column);
            } else {
                editor.moveCursorTo(0, 0);
            }
            editor.clearSelection();
            editor.focus();
        },
        getValue() {
            return session.getValue();
        },
        setMode(pathValue, contentTypeValue) {
            session.setMode(`ace/mode/${inferMode(pathValue, contentTypeValue)}`);
        },
        resize() {
            editor.resize();
        },
        focus() {
            editor.focus();
        },
        destroy() {
            if (changeTimer) {
                clearTimeout(changeTimer);
                changeTimer = null;
            }

            editor.destroy();
            editor.container.remove();
        }
    };
}
