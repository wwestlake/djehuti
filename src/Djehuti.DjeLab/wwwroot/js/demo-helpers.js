// Demo system JavaScript helpers

window.dj_getElementRect = function(selector) {
    try {
        const el = document.querySelector(selector);
        if (!el) return null;
        const rect = el.getBoundingClientRect();
        return {
            left: rect.left,
            top: rect.top,
            width: rect.width,
            height: rect.height
        };
    } catch (e) {
        console.error('dj_getElementRect error:', e);
        return null;
    }
};

window.dj_getViewportWidth = function() {
    return document.documentElement.clientWidth;
};

window.dj_getViewportHeight = function() {
    return document.documentElement.clientHeight;
};

window.dj_recordDemoStep = function(stepData) {
    // Placeholder for recording demo steps as user interacts
    console.log('Demo step recorded:', stepData);
};
