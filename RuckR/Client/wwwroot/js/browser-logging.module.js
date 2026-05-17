let dotNetLogger;
let originalConsoleError;

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

function serializeConsoleArg(arg) {
  if (arg instanceof Error) {
    return `${arg.message}\n${arg.stack || ''}`;
  }

  if (typeof arg === 'string') {
    return arg;
  }

  try {
    return JSON.stringify(arg);
  } catch {
    return String(arg);
  }
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

function onConsoleError(...args) {
  report(`console.error: ${args.map(serializeConsoleArg).join(' ')}`, null);
  originalConsoleError.apply(console, args);
}

export function start(logger) {
  dotNetLogger = logger;
  if (!originalConsoleError) {
    originalConsoleError = console.error;
    console.error = onConsoleError;
  }
  window.addEventListener('error', onError);
  window.addEventListener('unhandledrejection', onUnhandledRejection);
}

export function stop() {
  window.removeEventListener('error', onError);
  window.removeEventListener('unhandledrejection', onUnhandledRejection);
  if (originalConsoleError) {
    console.error = originalConsoleError;
    originalConsoleError = undefined;
  }
  dotNetLogger = undefined;
}
