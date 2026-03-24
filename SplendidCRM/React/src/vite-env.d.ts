/// <reference types="vite/client" />

// Augment Window interface for runtime config
interface Window {
  __SPLENDID_CONFIG__?: {
    API_BASE_URL?: string;
    SIGNALR_URL?: string;
    ENVIRONMENT?: string;
  };
  cordova?: any;
  splendidBaseUrl?: string;
  reactHistory?: any;
}
