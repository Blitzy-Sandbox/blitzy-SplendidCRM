# ESM Conversion Exceptions Log

## SplendidCRM React 19 / Vite Migration — Prompt 2 of 3

This document logs any application source files that retain `require()` calls after the ESM conversion, with justification for each exception.

### Scan Results

A comprehensive scan of all application source files (excluding `node_modules/` and `ckeditor5-custom-build/`) was performed.

### Exceptions

**No exceptions.** All `require()` calls in application source files have been converted to ESM `import` statements or commented out (BPMN BusinessProcesses files where the `require()` calls were already deactivated by previous agents).

### Verification Commands

```bash
# Verify zero active require() in application source
grep -rn "require(" SplendidCRM/React/src/ \
  --include="*.ts" --include="*.tsx" \
  | grep -v "node_modules" \
  | grep -v "ckeditor5-custom-build" \
  | grep -v "^.*:.*//.*require(" \
  | grep -v "^.*:.*\*.*require("

# Expected output: empty (no results)
```

### Summary

- **Total files scanned:** 763 TypeScript/TSX files
- **Active `require()` calls remaining:** 0
- **Documented exceptions:** 0
- **Commented-out `require()` calls:** ~40 in `BusinessProcesses/` files (replaced by ESM imports by previous agents, old `require()` lines preserved as comments for reference)
