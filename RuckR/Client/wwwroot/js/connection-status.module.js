let dotNetRef = null;

function handleOffline() {
    dotNetRef?.invokeMethodAsync('OnBrowserOffline');
}

function handleOnline() {
    dotNetRef?.invokeMethodAsync('OnBrowserOnline');
}

export function subscribe(ref) {
    unsubscribe();
    dotNetRef = ref;
    window.addEventListener('offline', handleOffline);
    window.addEventListener('online', handleOnline);
}

export function unsubscribe() {
    window.removeEventListener('offline', handleOffline);
    window.removeEventListener('online', handleOnline);
    dotNetRef = null;
}
