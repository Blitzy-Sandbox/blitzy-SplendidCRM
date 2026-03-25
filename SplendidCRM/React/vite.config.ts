/*
 * SplendidCRM React 19 / Vite 6.x Build Configuration
 *
 * This single configuration file replaces ALL 6 Webpack configuration files:
 *   1. configs/webpack/common.js   — Shared loaders, plugins, externals
 *   2. configs/webpack/dev_local.js — Dev server proxy to localhost
 *   3. configs/webpack/dev_remote.js — Dev server proxy to remote server
 *   4. configs/webpack/prod.js      — Production build (unminified)
 *   5. configs/webpack/prod_minimize.js — Production build (minified)
 *   6. configs/webpack/mobile.js    — Cordova mobile build
 *
 * Key architectural changes from Webpack:
 *   - TypeScript transpilation: ts-loader + thread-loader → Vite's native esbuild
 *   - CSS processing: style-loader/css-loader/postcss-loader/sass-loader → Vite built-in
 *   - Asset handling: file-loader/url-loader/svg-inline-loader → Vite built-in
 *   - Type checking: ForkTsCheckerWebpackPlugin → separate `tsc --noEmit` script
 *   - HTML template: HtmlWebpackPlugin + index.html.ejs → Vite HTML entry (index.html)
 *   - PWA manifest: WebpackPwaManifest → static public/manifest.json
 *   - Process polyfill: webpack.ProvidePlugin → Vite define
 *   - Externals: Webpack externals function → Rollup external config
 *   - Bundle output: Single SteviaCRM.js → Vite chunked ESM output
 */

import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  /*
   * Root directory for the Vite project.
   * Resolves to the SplendidCRM/React/ directory where index.html lives.
   */
  root: path.resolve(__dirname),

  /*
   * Plugins configuration.
   *
   * @vitejs/plugin-react provides:
   *   - Fast Refresh (replaces Webpack HMR + react-hot-loader)
   *   - JSX/TSX transformation
   *   - Babel integration for decorator transpilation
   *
   * CRITICAL: The Babel plugin configuration for MobX decorators is NON-NEGOTIABLE.
   * Without @babel/plugin-proposal-decorators (legacy: true) and
   * @babel/plugin-proposal-class-properties (loose: true), ALL MobX decorators
   * (@observable, @action, @computed) in 7+ files including Credentials.ts will
   * SILENTLY FAIL at runtime — properties won't be observable and the UI won't
   * react to state changes.
   */
  plugins: [
    react({
      babel: {
        plugins: [
          ['@babel/plugin-proposal-decorators', { legacy: true }],
          ['@babel/plugin-proposal-class-properties', { loose: true }],
        ],
      },
    }),
  ],

  /*
   * Module resolution configuration.
   * Matches the original Webpack resolve.extensions from common.js.
   * Vite resolves .ts and .tsx by default, but we explicitly list all extensions
   * to maintain exact parity with the Webpack configuration.
   */
  resolve: {
    extensions: ['.ts', '.tsx', '.js', '.jsx'],
  },

  /*
   * Global constant definitions — replaces webpack.ProvidePlugin and webpack.DefinePlugin.
   *
   * The original Webpack config used:
   *   new webpack.ProvidePlugin({ process: 'process/browser' })
   *   new webpack.DefinePlugin({ 'process.env.PATH': JSON.stringify(env.PATH) })
   *
   * In Vite, `define` replaces compile-time constants. Setting 'process.env' to '{}'
   * prevents "process is not defined" runtime errors when code references process.env
   * (e.g., process.env.PATH in index.tsx). The empty object ensures that property
   * access like process.env.ANYTHING evaluates to undefined rather than throwing.
   */
  define: {
    'process.env': '{}',
  },

  /*
   * Development server configuration.
   * Replaces configs/webpack/dev_local.js (port 3000, proxy to backend).
   *
   * The original Webpack dev server proxied /SplendidCRM/** → http://localhost:80.
   * The modernized backend runs ASP.NET Core on Kestrel at port 5000 with new
   * URL patterns (no /SplendidCRM prefix). Four proxy paths cover all backend
   * communication:
   *   - /Rest.svc — 152 REST API endpoints
   *   - /Administration/Rest.svc — 65 admin API endpoints
   *   - /hubs — SignalR WebSocket hubs (chat, twilio, phoneburner)
   *   - /api — Health check and other API endpoints
   *
   * Port 3000 is maintained for developer familiarity (Webpack dev server used 3000).
   */
  server: {
    port: 3000,
    proxy: {
      '/Rest.svc': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/Administration/Rest.svc': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        ws: true, // CRITICAL: WebSocket upgrade support for SignalR hub connections
      },
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },

  /*
   * Production build configuration.
   * Replaces configs/webpack/prod.js and prod_minimize.js.
   *
   * Key differences from Webpack:
   *   - Output: Vite produces hashed, chunked ESM files instead of a single SteviaCRM.js
   *   - Minification: Vite minifies by default in production (esbuild), replacing TerserPlugin
   *   - Source maps: Enabled for production debugging (matching Webpack prod.js devtool: 'source-map')
   *   - Externals: xlsx, canvg, pdfmake are externalized via Rollup (loaded via CDN/external scripts)
   *   - Manual chunks: vendor (react/react-dom/react-router) and mobx split for optimal caching
   */
  build: {
    outDir: 'dist',
    sourcemap: true,
    commonjsOptions: {
      include: [/node_modules/, /ckeditor5-custom-build/],
    },
    rollupOptions: {
      external: ['xlsx', 'canvg', 'pdfmake'],
      output: {
        globals: {
          xlsx: 'XLSX',
          canvg: 'canvg',
          pdfmake: 'pdfMake',
        },
        manualChunks: {
          vendor: ['react', 'react-dom', 'react-router'],
          mobx: ['mobx', 'mobx-react'],
        },
      },
    },
  },

  /*
   * Dependency pre-bundling configuration.
   *
   * CRITICAL: @babel/standalone MUST be explicitly included in optimizeDeps.
   * This package is a PRODUCTION dependency used by DynamicLayout_Compile.ts for
   * runtime in-browser TSX compilation of metadata-driven UI components. Without
   * explicit inclusion:
   *   - Vite's dependency optimizer might skip or tree-shake it
   *   - The runtime compilation system will fail, breaking all dynamic layout rendering
   *   - The Module Builder and Dynamic Layout Editor will not function
   *
   * Vite automatically detects and pre-bundles most CJS dependencies. The include
   * directive forces pre-bundling for packages that might otherwise be missed.
   */
  optimizeDeps: {
    include: ['@babel/standalone', 'ckeditor5-custom-build'],
  },

  /*
   * CSS configuration — replaces the entire Webpack CSS loader chain.
   *
   * Original Webpack chain:
   *   CSS:  style-loader → css-loader (importLoaders: 1) → postcss-loader
   *   SCSS: style-loader → css-loader → postcss-loader (autoprefixer) → sass-loader
   *
   * Vite handles all of this natively:
   *   - CSS imports are processed automatically
   *   - PostCSS configuration is read from postcss.config.js if present
   *   - SCSS/Sass files are compiled using the `sass` package (Dart Sass)
   *     which replaces the deprecated node-sass 9.0.0
   *   - Only 1 SCSS file (index.scss) and 17 CSS files exist in the project
   *
   * The scss preprocessorOptions block is kept for forward compatibility with
   * Dart Sass configuration (e.g., additionalData, silenceDeprecations).
   */
  css: {
    preprocessorOptions: {
      scss: {
        api: 'modern-compiler',
      },
    },
  },
});
