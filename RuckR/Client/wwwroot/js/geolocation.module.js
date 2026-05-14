let watchId = null;
let onlineStatusHandler = null;

function createGeolocationService() {
    return {
        getCurrentPosition,
        watchPosition,
        clearWatch,
        watchOnlineStatus,
        clearOnlineStatusWatch
    };
}

function getCurrentPosition(options) {
    return new Promise((resolve, reject) => {
        navigator.geolocation.getCurrentPosition(
            pos => resolve({
                coords: {
                    latitude: pos.coords.latitude,
                    longitude: pos.coords.longitude,
                    accuracy: pos.coords.accuracy
                },
                timestamp: pos.timestamp
            }),
            err => reject(err),
            options
        );
    });
}

function watchPosition(dotNetHelper, options) {
    watchId = navigator.geolocation.watchPosition(
        pos => dotNetHelper.invokeMethodAsync('OnPositionFromJs', {
            coords: {
                latitude: pos.coords.latitude,
                longitude: pos.coords.longitude,
                accuracy: pos.coords.accuracy
            },
            timestamp: pos.timestamp
        }),
        err => dotNetHelper.invokeMethodAsync('OnErrorFromJs', err.code, err.message),
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
    getCurrentPosition,
    watchPosition,
    clearWatch,
    watchOnlineStatus,
    clearOnlineStatusWatch
};
