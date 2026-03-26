/**
 * Runtime Configuration Loader — SplendidCRM
 *
 * Loads /config.json SYNCHRONOUSLY before any ES-module scripts execute.
 * This guarantees that window.__SPLENDID_CONFIG__ is fully populated
 * before React initialisation — preventing a race condition where Vite's
 * modulepreload in production could start module evaluation before an
 * async fetch completes.
 *
 * The same build artifact works in every environment (development, staging,
 * production) without rebuilding — only the contents of config.json change.
 *
 * Externalised into its own file so that the CSP policy does not require
 * 'unsafe-inline' in script-src.
 */
(function () {
  'use strict';

  // Safe defaults — used when config.json is unreachable (e.g. first-time
  // local dev before the file is created, or offline / Cordova scenarios).
  window.__SPLENDID_CONFIG__ = {
    API_BASE_URL: '',
    SIGNALR_URL: '',
    ENVIRONMENT: 'development'
  };

  try {
    var xhr = new XMLHttpRequest();
    xhr.open('GET', '/config.json', false); // Synchronous request — intentional
    xhr.send();
    if (xhr.status === 200) {
      window.__SPLENDID_CONFIG__ = JSON.parse(xhr.responseText);
    }
  } catch (err) {
    console.warn('Failed to load config.json, using defaults:', err);
  }
})();
