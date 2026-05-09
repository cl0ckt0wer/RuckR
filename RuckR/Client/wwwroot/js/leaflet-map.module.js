// ES module for Leaflet map integration
let map = null;
let pitchMarkers = [];
let userMarker = null;

export function initMap(containerId, centerLat, centerLng, zoom) {
    if (map) { map.remove(); }

    if (!window.L) {
        const container = document.getElementById(containerId);
        if (container) {
            container.classList.add('leaflet-container');
        }
        return false;
    }
    
    map = L.map(containerId).setView([centerLat, centerLng], zoom);
    
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        maxZoom: 19
    }).addTo(map);
    
    return true;
}

export function addPitchMarkers(markersJson, dotNetRef) {
    if (!map || !window.L) { return; }

    clearPitchMarkers();
    const markers = JSON.parse(markersJson);
    
    markers.forEach(m => {
        const marker = L.marker([m.latitude, m.longitude], {
            icon: L.divIcon({
                className: 'pitch-marker',
                html: '🏉',
                iconSize: [30, 30],
                iconAnchor: [15, 15]
            })
        }).addTo(map);
        
        marker.bindPopup(`<b>${m.name}</b><br>${m.type} Pitch`);
        
        marker.on('click', () => {
            if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnPitchClicked', m.id);
            }
        });
        
        pitchMarkers.push(marker);
    });
}

export function addUserMarker(lat, lng) {
    if (!map || !window.L) { return; }

    if (userMarker) {
        userMarker.setLatLng([lat, lng]);
    } else {
        userMarker = L.circleMarker([lat, lng], {
            radius: 8,
            fillColor: '#3388ff',
            color: '#ffffff',
            weight: 2,
            opacity: 1,
            fillOpacity: 0.8
        }).addTo(map);
        
        // Add pulse effect via CSS class
        userMarker.getElement().classList.add('user-location-pulse');
    }
}

export function centerOn(lat, lng) {
    if (map) {
        map.setView([lat, lng], map.getZoom());
    }
}

export function clearPitchMarkers() {
    if (!map) { return; }

    pitchMarkers.forEach(m => map.removeLayer(m));
    pitchMarkers = [];
}

export function dispose() {
    if (map) {
        map.remove();
        map = null;
    }
    pitchMarkers = [];
    userMarker = null;
}
