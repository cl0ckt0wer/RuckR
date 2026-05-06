// ES module for browser Geolocation API
let watchId = null;

export function getCurrentPosition() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject('Geolocation not supported');
            return;
        }
        navigator.geolocation.getCurrentPosition(resolve, reject, {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 5000
        });
    });
}

export function startWatch(dotNetRef) {
    if (!navigator.geolocation) {
        dotNetRef.invokeMethodAsync('OnPositionError', 'Geolocation not supported');
        return -1;
    }
    watchId = navigator.geolocation.watchPosition(
        (pos) => {
            dotNetRef.invokeMethodAsync('OnPositionChanged',
                pos.coords.latitude,
                pos.coords.longitude,
                pos.coords.accuracy);
        },
        (err) => {
            dotNetRef.invokeMethodAsync('OnPositionError', err.message);
        },
        {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 5000
        }
    );
    return watchId;
}

export function stopWatch() {
    if (watchId != null) {
        navigator.geolocation.clearWatch(watchId);
        watchId = null;
    }
}
