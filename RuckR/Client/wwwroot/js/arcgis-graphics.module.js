/**
 * arcgis-graphics.module.js
 *
 * Direct ArcGIS JS API graphics management for RuckR map.
 * Bypasses GeoBlazor's GraphicsLayer component entirely — avoids
 * the InvalidChildElementException bug in GeoBlazor Core 4.4.4
 * where MapView.RegisterChildComponent rejects GraphicsLayer.
 *
 * Uses the ArcGIS MapView's built-in `graphics` collection
 * (no separate GraphicsLayer needed at the JS level).
 *
 * Author: RuckR team
 */

let _view = null;
let _viewReady = false;
let _viewPromise = null;

// ── View acquisition ──────────────────────────────────────────

/**
 * Poll for the ArcGIS MapView instance created by GeoBlazor.
 * GeoBlazor creates a container div and ArcGIS attaches the view to it.
 * We locate the .esri-view element and resolve its parent MapView via
 * the ArcGIS JS API's internal __esriViewId registry.
 */
function findArcGisView() {
    if (_view) return _view;

    // ArcGIS MapView creates a surface div inside the container.
    const esriViewEl = document.querySelector('.esri-view-surface');
    if (!esriViewEl) return null;

    // Walk up to the view root and try several known access patterns
    let node = esriViewEl.parentElement;
    while (node) {
        // Pattern 1: Some ArcGIS components expose view on the root element
        if (node.view && node.view.type === 'map-view') {
            _view = node.view;
            return _view;
        }
        // Pattern 2: arcgis-map web component (used by map-components)
        if (node.tagName === 'ARCGIS-MAP' && node.view) {
            _view = node.view;
            return _view;
        }
        node = node.parentElement;
    }

    // Pattern 3: Try require() to get the view from the ArcGIS module registry.
    // esri/views/MapView constructor stores instances; we can iterate.
    if (window.require) {
        try {
            // The ArcGIS AMD loader's internal registry
            const arcgisMapEl = document.querySelector('arcgis-map');
            if (arcgisMapEl && arcgisMapEl.view) {
                _view = arcgisMapEl.view;
                return _view;
            }
        } catch(e) { /* AMD loader not available yet */ }
    }

    return null;
}

/**
 * Returns a promise that resolves with the ArcGIS MapView.
 * Retries for up to 15 seconds (30 polls x 500ms).
 */
export async function getMapViewAsync() {
    if (_view) return _view;
    if (_viewPromise) return _viewPromise;

    _viewPromise = new Promise((resolve, reject) => {
        let attempts = 0;
        const maxAttempts = 30; // 15 seconds

        function poll() {
            const v = findArcGisView();
            if (v) {
                _viewReady = true;
                resolve(v);
                return;
            }
            attempts++;
            if (attempts >= maxAttempts) {
                reject(new Error('Timed out waiting for ArcGIS MapView'));
                return;
            }
            setTimeout(poll, 500);
        }

        poll();
    });

    return _viewPromise;
}

// ── Symbol helpers ────────────────────────────────────────────

function createMarkerSymbol(color, size, outlineColor, outlineWidth) {
    // Use ArcGIS CIMSymbol or SimpleMarkerSymbol via require
    return {
        type: 'simple-marker',
        color: color,
        size: size + 'px',
        outline: {
            color: outlineColor || [255, 255, 255, 1],
            width: outlineWidth || 1.5
        }
    };
}

function createTextSymbol(text, color, size) {
    return {
        type: 'text',
        text: text,
        color: color || [255, 255, 255, 1],
        haloColor: [0, 0, 0, 0.5],
        haloSize: '1px',
        font: {
            size: size || 10,
            family: 'sans-serif',
            weight: 'bold'
        },
        yoffset: -(size || 10)
    };
}

// ── Public API ────────────────────────────────────────────────

/**
 * Add pitch markers to the map. Replaces all existing pitch graphics.
 * @param {Array} pitches - [{id, name, latitude, longitude, type}, ...]
 */
export async function updatePitchGraphics(pitches) {
    const view = await getMapViewAsync();
    if (!view) return;

    // Remove existing pitch graphics (tracked by a custom attribute)
    const toRemove = view.graphics.filter(g => g.attributes && g.attributes._ruckrType === 'pitch');
    view.graphics.removeMany(toRemove);

    if (!pitches || pitches.length === 0) return;

    const graphics = pitches.map(p => {
        const color = [30, 144, 255, 1]; // dodger blue
        const symbol = createMarkerSymbol(color, 14, [255, 255, 255, 1], 2);

        const point = {
            type: 'point',
            longitude: p.longitude,
            latitude: p.latitude
        };

        const graphic = {
            geometry: point,
            symbol: symbol,
            attributes: {
                _ruckrType: 'pitch',
                _ruckrId: p.id,
                name: p.name,
                type: p.type
            },
            popupTemplate: {
                title: p.name,
                content: (p.type || 'Standard') + ' Pitch'
            }
        };

        return graphic;
    });

    view.graphics.addMany(graphics);
}

/**
 * Add encounter markers to the map. Replaces all existing encounter graphics.
 * @param {Array} encounters - [{encounterId, name, latitude, longitude, level, rarity, position, successChancePercent}, ...]
 */
export async function updateEncounterGraphics(encounters) {
    const view = await getMapViewAsync();
    if (!view) return;

    // Remove existing encounter graphics
    const toRemove = view.graphics.filter(g => g.attributes && g.attributes._ruckrType === 'encounter');
    view.graphics.removeMany(toRemove);

    if (!encounters || encounters.length === 0) return;

    const rarityColors = {
        'Common': [150, 150, 150, 1],
        'Uncommon': [0, 200, 83, 1],
        'Rare': [30, 144, 255, 1],
        'Epic': [150, 50, 255, 1],
        'Legendary': [255, 215, 0, 1]
    };

    const graphics = encounters.map(e => {
        const color = rarityColors[e.rarity] || [200, 200, 200, 1];
        const symbol = createMarkerSymbol(color, 12, [255, 255, 255, 1], 1.5);

        const point = {
            type: 'point',
            longitude: e.longitude,
            latitude: e.latitude
        };

        return {
            geometry: point,
            symbol: symbol,
            attributes: {
                _ruckrType: 'encounter',
                _ruckrId: e.encounterId,
                playerId: e.playerId,
                name: e.name,
                level: e.level,
                rarity: e.rarity,
                position: e.position,
                successChancePercent: e.successChancePercent
            },
            popupTemplate: {
                title: e.name,
                content: 'Lv ' + e.level + ' · ' + e.rarity +
                         '<br/>' + (e.successChancePercent || 0) + '% success'
            }
        };
    });

    view.graphics.addMany(graphics);
}

/**
 * Update the user's position marker. Only one user marker at a time.
 * @param {number} latitude
 * @param {number} longitude
 */
export async function updateUserGraphic(latitude, longitude) {
    const view = await getMapViewAsync();
    if (!view) return;

    // Remove existing user graphic
    const toRemove = view.graphics.filter(g => g.attributes && g.attributes._ruckrType === 'user');
    view.graphics.removeMany(toRemove);

    if (latitude == null || longitude == null) return;

    const point = {
        type: 'point',
        longitude: longitude,
        latitude: latitude
    };

    const symbol = createMarkerSymbol([50, 120, 255, 1], 10, [255, 255, 255, 1], 2);

    view.graphics.add({
        geometry: point,
        symbol: symbol,
        attributes: {
            _ruckrType: 'user'
        }
    });
}

/**
 * Clear all RuckR graphics (pitches, encounters, user) from the map.
 */
export async function clearAllRuckrGraphics() {
    const view = await getMapViewAsync();
    if (!view) return;

    const toRemove = view.graphics.filter(g =>
        g.attributes && (g.attributes._ruckrType === 'pitch' ||
                          g.attributes._ruckrType === 'encounter' ||
                          g.attributes._ruckrType === 'user')
    );
    view.graphics.removeMany(toRemove);
}

/**
 * Find the closest pitch graphic to a clicked point.
 * Returns { pitchId, distanceMeters } or null.
 * @param {number} clickLat
 * @param {number} clickLng
 * @param {number} thresholdMeters
 */
export async function findClosestPitchAt(clickLat, clickLng, thresholdMeters) {
    const view = await getMapViewAsync();
    if (!view) return null;

    const pitchGraphics = view.graphics.filter(g =>
        g.attributes && g.attributes._ruckrType === 'pitch'
    );

    if (pitchGraphics.length === 0) return null;

    let closestId = null;
    let closestDist = thresholdMeters || 30;

    for (const g of pitchGraphics) {
        const geom = g.geometry;
        if (!geom || geom.type !== 'point') continue;

        const dist = haversineDistance(clickLat, clickLng, geom.latitude, geom.longitude);
        if (dist < closestDist) {
            closestDist = dist;
            closestId = g.attributes._ruckrId;
        }
    }

    if (closestId == null) return null;
    return { pitchId: closestId, distanceMeters: Math.round(closestDist * 10) / 10 };
}

/**
 * Haversine distance in meters between two lat/lng points.
 */
function haversineDistance(lat1, lng1, lat2, lng2) {
    const R = 6371000; // Earth radius in meters
    const dLat = (lat2 - lat1) * Math.PI / 180;
    const dLng = (lng2 - lng1) * Math.PI / 180;
    const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
              Math.cos(lat1 * Math.PI / 180) * Math.cos(lat2 * Math.PI / 180) *
              Math.sin(dLng / 2) * Math.sin(dLng / 2);
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return R * c;
}
