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

function baseTrace(chartType) {
    switch (chartType) {
        case 'line': return { type: 'scatter', mode: 'lines', x: [], y: [], line: { color: '#3fa9f5' } };
        case 'scatter': return { type: 'scatter', mode: 'markers', x: [], y: [], marker: { color: '#3fa9f5' } };
        case 'bar': return { type: 'bar', x: [], y: [], marker: { color: '#3fa9f5' } };
        case 'histogram': return { type: 'histogram', x: [], marker: { color: '#3fa9f5' } };
        case 'scatter3d': return { type: 'scatter3d', mode: 'lines+markers', x: [], y: [], z: [], line: { color: '#3fa9f5' }, marker: { color: '#3fa9f5', size: 3 } };
        case 'surface': return { type: 'surface', z: [], colorscale: 'Viridis' };
        default: throw new Error(`Unknown chart type: ${chartType}`);
    }
}

export function createChart(elementId, chartType, xLabel, yLabel, zLabel) {
    counters.delete(elementId);
    const el = document.getElementById(elementId);
    if (!el) return;
    Plotly.newPlot(el, [baseTrace(chartType)], baseLayout(chartType, xLabel, yLabel, zLabel), { responsive: true, displaylogo: false });
}

// point is already-parsed JSON: a number, or an array of 2-3 numbers (or,
// for "surface", an array representing one full row of z values).
export function addPoint(elementId, chartType, point) {
    const el = document.getElementById(elementId);
    if (!el || !el.data || el.data.length === 0) return;

    const asArray = Array.isArray(point) ? point : [point];

    switch (chartType) {
        case 'line':
        case 'scatter':
        case 'bar': {
            const [x, y] = asArray.length >= 2 ? asArray : [nextIndex(elementId), asArray[0]];
            Plotly.extendTraces(el, { x: [[x]], y: [[y]] }, [0]);
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
    const el = document.getElementById(elementId);
    if (el) Plotly.purge(el);
}
