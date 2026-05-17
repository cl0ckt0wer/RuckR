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
        }
    });
}
