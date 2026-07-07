// Thin wrapper around the global Plotly (loaded via CDN in index.html) for
// the Graph pane. Points arrive one at a time via addPoint, called on every
// emit(...) from a running Spinoza program -- Plotly.extendTraces appends to
// the existing trace in place instead of a full redraw, which is what makes
// the chart genuinely build up live rather than jump/flash on every update.
//
// A running counter (per element) supplies an auto x-axis for chart types
// fed a bare number instead of a [x, y] / [x, y, z] point, and supplies the
// row index for "surface", where each emitted vector is one full row of z
// values rather than a single point.
const counters = new Map();

// line/scatter/bar support multiple series in one chart: emit([x, y1, y2,
// ..., yN]) (more than 2 elements) plots N separate colored lines/series
// sharing the same x, instead of just one. The series count isn't known
// until the first point arrives (Spinoza vectors are dynamically shaped),
// so it's discovered lazily and extra traces are added on the fly the
// moment a wider-than-2 point shows up.
const seriesCounts = new Map();
const seriesPalette = ['#3fa9f5', '#e8b354', '#58d68d', '#ff6b81', '#a78bfa', '#4dd0e1', '#f472b6', '#fbbf24'];

function nextIndex(elementId) {
    const n = counters.get(elementId) ?? 0;
    counters.set(elementId, n + 1);
    return n;
}

function baseLayout(chartType, xLabel, yLabel, zLabel) {
    const is3d = chartType === 'scatter3d' || chartType === 'surface';
    const layout = {
        paper_bgcolor: 'transparent',
        plot_bgcolor: 'transparent',
        font: { color: '#dbe6f0' },
        margin: { l: 48, r: 16, t: 16, b: 40 },
        showlegend: false,
    };
    if (is3d) {
        layout.scene = {
            xaxis: { title: xLabel || 'x', gridcolor: '#2a3f52' },
            yaxis: { title: yLabel || 'y', gridcolor: '#2a3f52' },
            zaxis: { title: zLabel || 'z', gridcolor: '#2a3f52' },
            bgcolor: 'transparent',
        };
    } else {
        layout.xaxis = { title: xLabel || 'x', gridcolor: '#2a3f52' };
        layout.yaxis = { title: yLabel || 'y', gridcolor: '#2a3f52' };
    }
    return layout;
}

function baseTrace(chartType, color, name) {
    switch (chartType) {
        case 'line': return { type: 'scatter', mode: 'lines', x: [], y: [], name, line: { color } };
        case 'scatter': return { type: 'scatter', mode: 'markers', x: [], y: [], name, marker: { color } };
        case 'bar': return { type: 'bar', x: [], y: [], name, marker: { color } };
        case 'histogram': return { type: 'histogram', x: [], marker: { color } };
        case 'scatter3d': return { type: 'scatter3d', mode: 'lines+markers', x: [], y: [], z: [], line: { color }, marker: { color, size: 3 } };
        case 'surface': return { type: 'surface', z: [], colorscale: 'Viridis' };
        default: throw new Error(`Unknown chart type: ${chartType}`);
    }
}

export function createChart(elementId, chartType, xLabel, yLabel, zLabel) {
    counters.delete(elementId);
    seriesCounts.delete(elementId);
    const el = document.getElementById(elementId);
    if (!el) return;
    Plotly.newPlot(el, [baseTrace(chartType, seriesPalette[0], 'y1')], baseLayout(chartType, xLabel, yLabel, zLabel), { responsive: true, displaylogo: false });
}

// Only line/scatter/bar support multiple series. Adds (count - 1) more
// traces the first time `count` is seen for this element; a no-op on every
// later call once the count is already established, so this is cheap to
// call on every single point.
function ensureSeriesCount(el, elementId, chartType, count) {
    const known = seriesCounts.get(elementId) ?? 1;
    if (count <= known) return known;

    seriesCounts.set(elementId, count);
    const extraTraces = [];
    for (let i = known; i < count; i++) {
        extraTraces.push(baseTrace(chartType, seriesPalette[i % seriesPalette.length], `y${i + 1}`));
    }
    Plotly.addTraces(el, extraTraces);
    Plotly.relayout(el, { showlegend: true });
    return count;
}

// point is already-parsed JSON: a number, or an array of 2+ numbers (or,
// for "surface", an array representing one full row of z values).
export function addPoint(elementId, chartType, point) {
    const el = document.getElementById(elementId);
    if (!el || !el.data || el.data.length === 0) return;

    const asArray = Array.isArray(point) ? point : [point];

    switch (chartType) {
        case 'line':
        case 'scatter':
        case 'bar': {
            if (asArray.length <= 2) {
                const [x, y] = asArray.length === 2 ? asArray : [nextIndex(elementId), asArray[0]];
                Plotly.extendTraces(el, { x: [[x]], y: [[y]] }, [0]);
                break;
            }
            // [x, y1, y2, ..., yN] -- N series sharing one x.
            const [x, ...ys] = asArray;
            const seriesCount = ensureSeriesCount(el, elementId, chartType, ys.length);
            const traceIndices = Array.from({ length: seriesCount }, (_, i) => i);
            const xUpdate = traceIndices.map(() => [x]);
            const yUpdate = ys.map((y) => [y]);
            Plotly.extendTraces(el, { x: xUpdate, y: yUpdate }, traceIndices);
            break;
        }
        case 'histogram':
            Plotly.extendTraces(el, { x: [[asArray[0]]] }, [0]);
            break;
        case 'scatter3d': {
            const [x, y, z] = asArray.length >= 3 ? asArray : [nextIndex(elementId), asArray[0] ?? 0, asArray[1] ?? 0];
            Plotly.extendTraces(el, { x: [[x]], y: [[y]], z: [[z]] }, [0]);
            break;
        }
        case 'surface':
            // Each emitted vector is one full row; extendTraces appends it
            // as the next row of the z matrix.
            Plotly.extendTraces(el, { z: [[asArray]] }, [0]);
            break;
    }
}

export function dispose(elementId) {
    counters.delete(elementId);
    seriesCounts.delete(elementId);
    const el = document.getElementById(elementId);
    if (el) Plotly.purge(el);
}
