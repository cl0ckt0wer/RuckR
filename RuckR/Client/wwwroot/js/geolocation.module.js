let watchId = null;
let onlineStatusHandler = null;

function createGeolocationService() {
    return {
        getPermissionState,
        getCurrentPosition,
        watchPosition,
        clearWatch,
        watchOnlineStatus,
        clearOnlineStatusWatch
    };
}

async function getPermissionState() {
    if (!navigator.geolocation) {
        return "unavailable";
    }

    if (!navigator.permissions?.query) {
        return "unknown";
    }

    try {
        const status = await navigator.permissions.query({ name: "geolocation" });
        return status?.state || "unknown";
    } catch {
        return "unknown";
    }
}

function normalizeError(err) {
    const code = Number.isFinite(err?.code) ? err.code : 0;
    let message = err?.message || "Location is unavailable.";

    if (code === 1) {
        message = "Location permission was denied.";
    } else if (code === 2) {
        message = "Location is unavailable on this device.";
    } else if (code === 3) {
        message = "Location request timed out.";
    } else if (!navigator.geolocation) {
        message = "Geolocation is not supported by this browser.";
    }

    const error = new Error(message);
    error.code = code;
    return error;
}

function getCurrentPosition(options) {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(normalizeError({ code: 0 }));
            return;
        }

        navigator.geolocation.getCurrentPosition(
            pos => resolve({
                coords: {
                    latitude: pos.coords.latitude,
                    longitude: pos.coords.longitude,
                    accuracy: pos.coords.accuracy
                },
                timestamp: pos.timestamp
            }),
            err => reject(normalizeError(err)),
            options
        );
    });
}

function watchPosition(dotNetHelper, options) {
    if (!navigator.geolocation) {
        dotNetHelper.invokeMethodAsync(
            "OnErrorFromJs",
            0,
            "Geolocation is not supported by this browser.");
        return -1;
    }

    watchId = navigator.geolocation.watchPosition(
        pos => dotNetHelper.invokeMethodAsync('OnPositionFromJs', {
            coords: {
                latitude: pos.coords.latitude,
                longitude: pos.coords.longitude,
                accuracy: pos.coords.accuracy
            },
            timestamp: pos.timestamp
        }),
        err => {
            const normalized = normalizeError(err);
            dotNetHelper.invokeMethodAsync('OnErrorFromJs', normalized.code, normalized.message);
        },
        options
    );
    return watchId;
}

function clearWatch(id) {
    navigator.geolocation.clearWatch(id);
}

function watchOnlineStatus(dotNetHelper) {
    clearOnlineStatusWatch();

    onlineStatusHandler = () => dotNetHelper.invokeMethodAsync('OnBrowserOnlineStateChanged', navigator.onLine);
    window.addEventListener('online', onlineStatusHandler);
    window.addEventListener('offline', onlineStatusHandler);

    onlineStatusHandler();
}

function clearOnlineStatusWatch() {
    if (!onlineStatusHandler) {
        return;
    }

    window.removeEventListener('online', onlineStatusHandler);
    window.removeEventListener('offline', onlineStatusHandler);
    onlineStatusHandler = null;
}

export {
    createGeolocationService,
    getPermissionState,
    getCurrentPosition,
    watchPosition,
    clearWatch,
    watchOnlineStatus,
    clearOnlineStatusWatch
};
