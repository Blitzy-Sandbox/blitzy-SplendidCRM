/// <reference types="vite/client" />

// Augment Window interface for runtime config
// NOTE: splendidBaseUrl and reactHistory are declared in Router5.tsx with precise types.
// Do NOT redeclare them here to avoid TS2687/TS2717 conflicts.
interface Window {
  __SPLENDID_CONFIG__?: {
    API_BASE_URL?: string;
    SIGNALR_URL?: string;
    ENVIRONMENT?: string;
  };
  cordova?: any;
}
