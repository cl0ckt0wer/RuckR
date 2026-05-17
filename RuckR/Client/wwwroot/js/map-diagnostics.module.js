const recentConsole = [];
let consoleCaptureStarted = false;
let originalConsoleWarn;
let originalConsoleError;

function rememberConsole(level, args) {
    const text = args.map(arg => {
        if (arg instanceof Error) return `${arg.message}\n${arg.stack || ''}`;
        if (typeof arg === 'string') return arg;
        try { return JSON.stringify(arg); } catch { return String(arg); }
    }).join(' ');

    if (!/arcgis|esri|geoblazor|map|webgl|canvas/i.test(text)) {
        return;
    }

    recentConsole.push({
        level,
        text: text.slice(0, 800),
        timestamp: new Date().toISOString()
    });

    while (recentConsole.length > 30) {
        recentConsole.shift();
    }
}

export function startMapDiagnosticsCapture() {
    if (consoleCaptureStarted) return;
    consoleCaptureStarted = true;

    originalConsoleWarn = console.warn;
    originalConsoleError = console.error;

    console.warn = (...args) => {
        rememberConsole('warn', args);
        originalConsoleWarn.apply(console, args);
    };

    console.error = (...args) => {
        rememberConsole('error', args);
        originalConsoleError.apply(console, args);
    };
}

export function stopMapDiagnosticsCapture() {
    if (!consoleCaptureStarted) return;
    console.warn = originalConsoleWarn;
    console.error = originalConsoleError;
    consoleCaptureStarted = false;
}

function rectOf(element) {
    if (!element) return null;
    const rect = element.getBoundingClientRect();
    return {
        x: Math.round(rect.x * 100) / 100,
        y: Math.round(rect.y * 100) / 100,
        width: Math.round(rect.width * 100) / 100,
        height: Math.round(rect.height * 100) / 100,
        right: Math.round(rect.right * 100) / 100,
        bottom: Math.round(rect.bottom * 100) / 100
    };
}

function styleOf(element) {
    if (!element) return null;
    const style = getComputedStyle(element);
    return {
        display: style.display,
        position: style.position,
        width: style.width,
        height: style.height,
        minHeight: style.minHeight,
        overflow: style.overflow,
        visibility: style.visibility
    };
}

function isVisible(element) {
    if (!element) return false;
    const style = getComputedStyle(element);
    const rect = element.getBoundingClientRect();
    return style.display !== 'none'
        && style.visibility !== 'hidden'
        && rect.width > 0
        && rect.height > 0;
}

function appCssRulesLoaded() {
    const appSheet = [...document.styleSheets]
        .find(sheet => sheet.href && sheet.href.endsWith('/css/app.css'));

    if (!appSheet) {
        return { found: false, mapWrapperRule: false, error: null };
    }

    try {
        const rules = [...appSheet.cssRules].map(rule => rule.cssText);
        return {
            found: true,
            mapWrapperRule: rules.some(rule => rule.includes('.map-shell > .map-wrapper')),
            mapShellRule: rules.some(rule => rule.includes('.map-shell')),
            ruleCount: rules.length,
            error: null
        };
    } catch (error) {
        return {
            found: true,
            mapWrapperRule: false,
            mapShellRule: false,
            ruleCount: null,
            error: error instanceof Error ? error.message : String(error)
        };
    }
}

function webGlSummary() {
    const canvas = document.querySelector('canvas');
    if (!canvas) return { canvas: false, context: false };

    const context = canvas.getContext('webgl2') || canvas.getContext('webgl');
    if (!context) return { canvas: true, context: false };

    return {
        canvas: true,
        context: true,
        drawingBufferWidth: context.drawingBufferWidth,
        drawingBufferHeight: context.drawingBufferHeight,
        renderer: context.getParameter(context.RENDERER),
        vendor: context.getParameter(context.VENDOR)
    };
}

function elementStackAtMapCenter() {
    const map = document.querySelector('[data-testid="map-container"]');
    const rect = rectOf(map);
    if (!rect) return [];

    const x = rect.x + rect.width / 2;
    const y = rect.y + rect.height / 2;
    return document.elementsFromPoint(x, y).slice(0, 12).map(element => ({
        tag: element.tagName,
        id: element.id || null,
        className: element.className?.toString().slice(0, 180) || null,
        testId: element.getAttribute('data-testid'),
        text: element.innerText?.trim().slice(0, 120) || null,
        rect: rectOf(element),
        style: styleOf(element)
    }));
}

function readCanvasVisualSummary() {
    const canvas = document.querySelector('canvas');
    if (!canvas) {
        return { present: false };
    }

    const rect = rectOf(canvas);
    const summary = {
        present: true,
        rect,
        width: canvas.width,
        height: canvas.height,
        method: null,
        error: null,
        samples: 0,
        transparentRatio: null,
        whiteRatio: null,
        darkRatio: null,
        variedRatio: null,
        center: null
    };

    try {
        const context = canvas.getContext('webgl2') || canvas.getContext('webgl');
        if (!context || canvas.width <= 0 || canvas.height <= 0) {
            summary.error = 'No readable WebGL context or empty canvas.';
            return summary;
        }

        summary.method = 'webgl.readPixels';
        const points = [
            [0.5, 0.5],
            [0.25, 0.25],
            [0.75, 0.25],
            [0.25, 0.75],
            [0.75, 0.75],
            [0.5, 0.25],
            [0.5, 0.75],
            [0.25, 0.5],
            [0.75, 0.5]
        ];
        let transparent = 0;
        let white = 0;
        let dark = 0;
        let varied = 0;

        for (const [px, py] of points) {
            const x = Math.max(0, Math.min(canvas.width - 1, Math.round(canvas.width * px)));
            const y = Math.max(0, Math.min(canvas.height - 1, Math.round(canvas.height * py)));
            const pixel = new Uint8Array(4);
            context.readPixels(x, y, 1, 1, context.RGBA, context.UNSIGNED_BYTE, pixel);
            const [r, g, b, a] = pixel;
            if (px === 0.5 && py === 0.5) {
                summary.center = { r, g, b, a };
            }
            summary.samples++;
            if (a < 10) transparent++;
            if (r > 245 && g > 245 && b > 245 && a > 10) white++;
            if (r < 35 && g < 35 && b < 35 && a > 10) dark++;
            if ((Math.max(r, g, b) - Math.min(r, g, b) > 8 || r < 235 || g < 235 || b < 235) && a > 10) varied++;
        }

        summary.transparentRatio = transparent / summary.samples;
        summary.whiteRatio = white / summary.samples;
        summary.darkRatio = dark / summary.samples;
        summary.variedRatio = varied / summary.samples;
    } catch (error) {
        summary.error = error instanceof Error ? error.message : String(error);
    }

    return summary;
}

function serializeError(error) {
    if (!error) return null;
    return {
        name: error.name || null,
        message: error.message || String(error),
        details: error.details || null
    };
}

function arcGisViewSummary() {
    const arcgisMap = document.querySelector('arcgis-map');
    const view = arcgisMap?.view;
    if (!view) {
        return { present: false };
    }

    let layerViews = null;
    try {
        layerViews = view.layerViews?.length ?? null;
    } catch {
        layerViews = null;
    }

    return {
        present: true,
        ready: view.ready ?? null,
        updating: view.updating ?? null,
        stationary: view.stationary ?? null,
        suspended: view.suspended ?? null,
        width: view.width ?? null,
        height: view.height ?? null,
        zoom: view.zoom ?? null,
        scale: view.scale ?? null,
        center: view.center ? {
            latitude: view.center.latitude ?? null,
            longitude: view.center.longitude ?? null,
            spatialReference: view.center.spatialReference?.wkid ?? null
        } : null,
        extent: view.extent ? {
            xmin: view.extent.xmin ?? null,
            ymin: view.extent.ymin ?? null,
            xmax: view.extent.xmax ?? null,
            ymax: view.extent.ymax ?? null,
            spatialReference: view.extent.spatialReference?.wkid ?? null
        } : null,
        fatalError: serializeError(view.fatalError),
        map: view.map ? {
            loaded: view.map.loaded ?? null,
            loadStatus: view.map.loadStatus ?? null,
            basemapId: view.map.basemap?.id ?? null,
            basemapTitle: view.map.basemap?.title ?? null,
            basemapLoaded: view.map.basemap?.loaded ?? null,
            layers: view.map.layers?.length ?? null,
            allLayers: view.map.allLayers?.length ?? null
        } : null,
        layerViews
    };
}

export function collectMapDiagnostics(reason) {
    const controls = [...document.querySelectorAll('.esri-ui .esri-widget, .esri-zoom, calcite-action')];
    const canvases = [...document.querySelectorAll('canvas')];
    const resources = performance.getEntriesByType('resource');
    const arcGisResources = resources.filter(entry =>
        entry.name.includes('arcgis')
        || entry.name.includes('GeoBlazor')
        || entry.name.includes('_content/dymaptic'));

    return JSON.stringify({
        reason,
        url: location.href,
        userAgent: navigator.userAgent,
        viewport: {
            width: window.innerWidth,
            height: window.innerHeight,
            dpr: window.devicePixelRatio,
            orientation: screen.orientation?.type ?? null
        },
        mapContainer: {
            rect: rectOf(document.querySelector('[data-testid="map-container"]')),
            style: styleOf(document.querySelector('[data-testid="map-container"]'))
        },
        mapWrapper: {
            rect: rectOf(document.querySelector('.map-wrapper')),
            style: styleOf(document.querySelector('.map-wrapper'))
        },
        arcgisMap: {
            rect: rectOf(document.querySelector('arcgis-map')),
            style: styleOf(document.querySelector('arcgis-map')),
            hydrated: document.querySelector('arcgis-map')?.hasAttribute('hydrated') ?? false
        },
        esriView: {
            rect: rectOf(document.querySelector('.esri-view')),
            style: styleOf(document.querySelector('.esri-view')),
            className: document.querySelector('.esri-view')?.className?.toString() ?? null
        },
        canvases: canvases.map(canvas => ({
            rect: rectOf(canvas),
            width: canvas.width,
            height: canvas.height,
            style: styleOf(canvas)
        })),
        controls: {
            count: controls.length,
            visible: controls.filter(isVisible).length
        },
        ui: {
            loadingVisible: isVisible(document.querySelector('[data-testid="map-loading"]')),
            gpsChipVisible: isVisible(document.querySelector('[data-testid="gps-status-chip"]')),
            gpsNoticeVisible: isVisible(document.querySelector('[data-testid="gps-readiness"]')),
            pitchLegendVisible: isVisible(document.querySelector('[data-testid="pitch-legend"]'))
        },
        css: {
            app: appCssRulesLoaded(),
            links: [...document.querySelectorAll('link[rel="stylesheet"]')].map(link => link.href)
        },
        webGl: webGlSummary(),
        resources: {
            total: resources.length,
            arcGisOrGeoBlazor: arcGisResources.length,
            slowestArcGisOrGeoBlazor: arcGisResources
                .sort((a, b) => b.duration - a.duration)
                .slice(0, 5)
                .map(entry => ({
                    name: entry.name,
                    duration: Math.round(entry.duration),
                    transferSize: entry.transferSize
                }))
        },
        visual: {
            canvas: readCanvasVisualSummary(),
            elementStackAtCenter: elementStackAtMapCenter()
        },
        arcgis: arcGisViewSummary(),
        console: {
            recent: recentConsole.slice(-12)
        }
    });
}
