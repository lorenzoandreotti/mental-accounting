import { build } from 'esbuild';
import { fileURLToPath } from 'node:url';
await build({
    absWorkingDir: fileURLToPath(new URL('../', import.meta.url)),
    entryPoints: [fileURLToPath(new URL('../Client/balance-chart.js', import.meta.url))],
    outfile: fileURLToPath(new URL('../wwwroot/js/balance-chart.bundle.js', import.meta.url)),
    tsconfigRaw: {},
    bundle: true,
    minify: true,
    format: 'esm',
    target: ['es2022'],
    legalComments: 'eof'
});
