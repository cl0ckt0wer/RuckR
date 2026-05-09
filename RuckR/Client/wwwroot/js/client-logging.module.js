let dotNetLogger;

function getUserAgent() {
  return navigator.userAgent || null;
}

function getUrl() {
  return window.location.href || null;
}

function report(message, stack) {
  if (!dotNetLogger) {
    return;
  }

  dotNetLogger.invokeMethodAsync('LogBrowserError', message || 'Unknown browser error', stack || null, getUrl(), getUserAgent())
    .catch(() => {
      // Logging must never create a second client-side failure.
    });
}

function onError(event) {
  const error = event.error;
  report(event.message, error && error.stack ? error.stack : null);
}

function onUnhandledRejection(event) {
  const reason = event.reason;
  if (reason instanceof Error) {
    report(reason.message, reason.stack);
    return;
  }

  report(`Unhandled promise rejection: ${String(reason)}`, null);
}

export function start(logger) {
  dotNetLogger = logger;
  window.addEventListener('error', onError);
  window.addEventListener('unhandledrejection', onUnhandledRejection);
}

export function stop() {
  window.removeEventListener('error', onError);
  window.removeEventListener('unhandledrejection', onUnhandledRejection);
  dotNetLogger = undefined;
}
