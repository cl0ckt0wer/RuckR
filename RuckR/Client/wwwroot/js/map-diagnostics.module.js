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

function visibleCanvasScore(canvas) {
    const rect = canvas.getBoundingClientRect();
    const style = getComputedStyle(canvas);
    if (style.display === 'none' || style.visibility === 'hidden' || rect.width <= 0 || rect.height <= 0) {
        return 0;
    }

    return rect.width * rect.height;
}

function getBestCanvas() {
    const canvases = [...document.querySelectorAll('canvas')];
    return canvases
        .map((canvas, index) => ({ canvas, index, score: visibleCanvasScore(canvas) }))
        .filter(item => item.score > 0)
        .sort((a, b) => b.score - a.score)[0] ?? null;
}

function webGlSummary() {
    const selected = getBestCanvas();
    const canvas = selected?.canvas;
    if (!canvas) return { canvas: false, context: false };

    const context = canvas.getContext('webgl2') || canvas.getContext('webgl');
    if (!context) return { canvas: true, context: false };

    return {
        canvas: true,
        selectedCanvasIndex: selected.index,
        selectedCanvasScore: selected.score,
        selectedCanvasRect: rectOf(canvas),
        canvasWidth: canvas.width,
        canvasHeight: canvas.height,
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

function readCanvasElementSummary() {
    const selected = getBestCanvas();
    const canvas = selected?.canvas;
    if (!canvas) {
        return { present: false };
    }

    const rect = rectOf(canvas);
    return {
        present: true,
        selectedCanvasIndex: selected.index,
        selectedCanvasScore: selected.score,
        rect,
        width: canvas.width,
        height: canvas.height
    };
}

function surfaceSummary() {
    const esriView = document.querySelector('.esri-view');
    const surface = document.querySelector('.esri-view-surface');
    const canvas = getBestCanvas()?.canvas ?? null;
    const viewRect = rectOf(esriView);
    const surfaceRect = rectOf(surface);
    const canvasRect = rectOf(canvas);

    return {
        viewVisible: isVisible(esriView),
        surfaceVisible: isVisible(surface),
        canvasVisible: isVisible(canvas),
        viewRect,
        surfaceRect,
        canvasRect,
        surfaceMatchesView: !!(viewRect && surfaceRect
            && Math.abs(viewRect.width - surfaceRect.width) < 2
            && Math.abs(viewRect.height - surfaceRect.height) < 2),
        canvasMatchesView: !!(viewRect && canvasRect
            && Math.abs(viewRect.width - canvasRect.width) < 4
            && Math.abs(viewRect.height - canvasRect.height) < 4)
    };
}

function healthSummary() {
    const arcgis = arcGisViewSummary();
    const surface = surfaceSummary();
    const webGl = webGlSummary();

    return {
        ready: !!arcgis.ready,
        basemapLoaded: !!arcgis.map?.basemapLoaded,
        viewSized: (arcgis.width ?? 0) > 0 && (arcgis.height ?? 0) > 0,
        surfaceVisible: surface.surfaceVisible,
        canvasVisible: surface.canvasVisible,
        webGlContext: !!webGl.context,
        hasFatalError: !!arcgis.fatalError,
        healthy: !!arcgis.ready
            && !!arcgis.map?.basemapLoaded
            && (arcgis.width ?? 0) > 0
            && (arcgis.height ?? 0) > 0
            && surface.surfaceVisible
            && surface.canvasVisible
            && !!webGl.context
            && !arcgis.fatalError
    };
}

function serializeError(error) {
    if (!error) return null;
    return {
        name: error.name || null,
        message: error.message || String(error),
        details: error.details || null
    };
}

function collectionItems(collection) {
    if (!collection) return [];
    if (Array.isArray(collection.items)) return collection.items;
    if (typeof collection.toArray === 'function') {
        try { return collection.toArray(); } catch { return []; }
    }
    return [];
}

function graphicSummary(graphic) {
    const geometry = graphic?.geometry;
    const attributes = graphic?.attributes ?? {};
    return {
        geometryType: geometry?.type ?? null,
        latitude: geometry?.latitude ?? null,
        longitude: geometry?.longitude ?? null,
        symbolType: graphic?.symbol?.type ?? null,
        symbolStyle: graphic?.symbol?.style ?? null,
        ruckrType: attributes._ruckrType ?? null,
        name: attributes.name ?? null,
        rarity: attributes.rarity ?? null,
        level: attributes.level ?? null,
        parkName: attributes.parkName ?? null,
        gpsQuality: attributes.gpsQuality ?? null,
        accuracyMeters: attributes.accuracyMeters ?? null
    };
}

function layerSummary(layer) {
    const graphics = collectionItems(layer?.graphics);
    return {
        id: layer?.id ?? null,
        title: layer?.title ?? null,
        type: layer?.type ?? null,
        visible: layer?.visible ?? null,
        opacity: layer?.opacity ?? null,
        listMode: layer?.listMode ?? null,
        loaded: layer?.loaded ?? null,
        loadStatus: layer?.loadStatus ?? null,
        graphicsCount: layer?.graphics?.length ?? graphics.length ?? null,
        sampleGraphics: graphics.slice(0, 5).map(graphicSummary)
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

    const layers = collectionItems(view.map?.layers);
    const allLayers = collectionItems(view.map?.allLayers);

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
            allLayers: view.map.allLayers?.length ?? null,
            layerSummaries: layers.map(layerSummary),
            allLayerSummaries: allLayers.map(layerSummary)
        } : null,
        layerViews
    };
}

export function collectMapDiagnostics(reason) {
    const controls = [...document.querySelectorAll('.esri-ui .esri-widget, .esri-zoom, calcite-action')];
    const canvases = [...document.querySelectorAll('canvas')];
    const resources = performance.getEntriesByType('resource');
    const url = new URL(location.href);
    const arcGisResources = resources.filter(entry =>
        entry.name.includes('arcgis')
        || entry.name.includes('GeoBlazor')
        || entry.name.includes('_content/dymaptic'));

    return JSON.stringify({
        reason,
        url: location.href,
        route: {
            path: location.pathname,
            search: location.search,
            debugMode: url.searchParams.get('mode'),
            basemap: url.searchParams.get('basemap'),
            mapGraphics: url.searchParams.get('mapGraphics'),
            autoGps: url.searchParams.get('autoGps'),
            mapDiagnostics: url.searchParams.get('mapDiagnostics'),
            arcGisWidgets: url.searchParams.get('arcGisWidgets')
        },
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
        canvases: canvases.map((canvas, index) => ({
            index,
            rect: rectOf(canvas),
            width: canvas.width,
            height: canvas.height,
            visibleScore: visibleCanvasScore(canvas),
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
            pitchLegendVisible: isVisible(document.querySelector('[data-testid="pitch-legend"]')),
            encounterRadarVisible: isVisible(document.querySelector('[data-testid="encounter-radar"]')),
            encounterOverlayVisible: isVisible(document.querySelector('[data-testid="encounter-overlay"]')),
            encounterCountText: document.querySelector('[data-testid="encounter-count"]')?.textContent?.trim() ?? null
        },
        css: {
            app: appCssRulesLoaded(),
            links: [...document.querySelectorAll('link[rel="stylesheet"]')].map(link => link.href)
        },
        health: healthSummary(),
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
            surface: surfaceSummary(),
            canvas: readCanvasElementSummary(),
            elementStackAtCenter: elementStackAtMapCenter()
        },
        arcgis: arcGisViewSummary(),
        console: {
            recent: recentConsole.slice(-12)
        }
    });
}
