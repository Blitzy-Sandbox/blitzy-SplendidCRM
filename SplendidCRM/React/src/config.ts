/*
 * This program is free software: you can redistribute it and/or modify it under the terms of the 
 * GNU Affero General Public License as published by the Free Software Foundation, either version 3 
 * of the License, or (at your option) any later version.
 * 
 * In accordance with Section 7(b) of the GNU Affero General Public License version 3, 
 * the Appropriate Legal Notices must display the following words on all interactive user interfaces: 
 * "Copyright (C) 2005-2022 SplendidCRM Software, Inc. All rights reserved."
 */

// ============================================================================
// SplendidCRM/React/src/config.ts
// Runtime Configuration Loader
//
// PURPOSE:
// Provides a typed runtime configuration singleton that reads from
// window.__SPLENDID_CONFIG__ (populated by the inline <script> in
// index.html that fetches /config.json BEFORE the app module loads).
//
// This module enables the SAME build artifact to run in any environment
// (development, staging, production) without rebuilding — all
// environment-specific values come from /config.json at runtime.
//
// DESIGN DECISIONS:
// 1. No fetch() call in this module — fetching is done by the inline
//    script in index.html before this module loads. This avoids
//    async initialization complexity.
// 2. SIGNALR_URL falls back to API_BASE_URL when empty or omitted
//    (AAP Section 0.4.5).
// 3. Trailing slashes are stripped from URLs because callers
//    (e.g., SplendidRequest.ts) prepend paths with leading slashes.
// 4. Both named exports (getConfig, initConfig) and a default export
//    (_config) are provided for flexible consumption patterns.
// 5. No side effects on import — config is initialized only when
//    initConfig() is explicitly called during app startup.
// ============================================================================

/**
 * Runtime configuration interface.
 * Values are loaded from /config.json via the inline script in index.html
 * and made available through window.__SPLENDID_CONFIG__.
 */
export interface AppConfig
{
	/** Base URL for the backend API (e.g., "http://localhost:5000"). */
	API_BASE_URL : string;
	/** SignalR hub base URL. Defaults to API_BASE_URL when empty. */
	SIGNALR_URL  : string;
	/** Environment identifier (e.g., "development", "staging", "production"). */
	ENVIRONMENT  : string;
}

// ---------------------------------------------------------------------------
// Default configuration — used when window.__SPLENDID_CONFIG__ is absent
// or when individual properties are missing from the runtime payload.
// All values are intentionally empty/neutral so the module never contains
// hardcoded environment-specific data.
// ---------------------------------------------------------------------------
const DEFAULT_CONFIG: AppConfig =
{
	API_BASE_URL : '',
	SIGNALR_URL  : '',
	ENVIRONMENT  : 'development',
};

// ---------------------------------------------------------------------------
// Module-level singleton holding the active configuration.
// Starts as a shallow copy of DEFAULT_CONFIG and is populated by initConfig().
// ---------------------------------------------------------------------------
let _config: AppConfig = { ...DEFAULT_CONFIG };

/**
 * Strips a trailing forward-slash from a URL string.
 * Callers such as SplendidRequest.ts prepend paths with leading slashes,
 * so the base URL must not end with one to avoid double-slash issues.
 *
 * @param url - The URL to normalize.
 * @returns The URL without a trailing slash.
 */
function stripTrailingSlash(url: string): string
{
	if ( url && url.endsWith('/') )
	{
		return url.slice(0, -1);
	}
	return url;
}

/**
 * Initializes the runtime configuration.
 *
 * Called **once** during app startup (in index.tsx) **before** React
 * rendering begins. Reads from `window.__SPLENDID_CONFIG__` which is
 * populated by the inline `<script>` in `index.html` that fetches
 * `/config.json`.
 *
 * If `window.__SPLENDID_CONFIG__` is not present (e.g., during unit
 * tests or when running without the config script), the module falls
 * back to DEFAULT_CONFIG gracefully.
 *
 * @returns The resolved AppConfig singleton.
 */
export function initConfig(): AppConfig
{
	// Access the global config payload injected by the index.html inline script.
	// The cast to `any` is intentional — __SPLENDID_CONFIG__ is set outside
	// TypeScript's type system by a plain <script> block.
	const windowConfig = (window as any).__SPLENDID_CONFIG__;

	if ( windowConfig && typeof windowConfig === 'object' )
	{
		_config =
		{
			API_BASE_URL : windowConfig.API_BASE_URL || DEFAULT_CONFIG.API_BASE_URL,
			// SIGNALR_URL falls back to API_BASE_URL when empty/omitted
			// (AAP Section 0.4.5).
			SIGNALR_URL  : windowConfig.SIGNALR_URL  || windowConfig.API_BASE_URL || DEFAULT_CONFIG.SIGNALR_URL,
			ENVIRONMENT  : windowConfig.ENVIRONMENT   || DEFAULT_CONFIG.ENVIRONMENT,
		};
	}
	else
	{
		// No runtime config available — reset to defaults.
		_config = { ...DEFAULT_CONFIG };
	}

	// Normalize URLs: strip trailing slashes so callers can safely
	// concatenate paths (e.g., config.API_BASE_URL + '/Rest.svc/...').
	_config.API_BASE_URL = stripTrailingSlash(_config.API_BASE_URL);
	_config.SIGNALR_URL  = stripTrailingSlash(_config.SIGNALR_URL);

	return _config;
}

/**
 * Returns the current runtime configuration.
 *
 * **Important:** This must be called **after** `initConfig()` has been
 * invoked during app startup. If called before initialization, it
 * returns the DEFAULT_CONFIG values (empty strings / 'development').
 *
 * @returns The active AppConfig singleton.
 */
export function getConfig(): AppConfig
{
	return _config;
}

// ---------------------------------------------------------------------------
// Default export — the config singleton object itself.
// Consumers can import the config directly:
//   import config from './config';
//   console.log(config.API_BASE_URL);
//
// Note: Because _config is reassigned (not mutated) inside initConfig(),
// the default export reference points to the INITIAL object. Consumers
// who import before initConfig() will hold a stale reference. For
// guaranteed correctness, prefer getConfig() for access after
// initialization, or import the named _config and use getConfig()
// to retrieve the latest reference.
// ---------------------------------------------------------------------------
export default _config;
