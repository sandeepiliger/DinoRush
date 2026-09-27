// Builds the game as ONE self-contained HTML file (JS, CSS and fonts inlined) for quick hosting/testing.
// Output: dist-single/dino-park-tycoon.html
import { build } from 'vite';
import fs from 'node:fs';
import path from 'node:path';

const out = 'dist-single/tmp';
await build({ logLevel: 'warn', build: { outDir: out, emptyOutDir: true, assetsInlineLimit: 100_000_000, cssCodeSplit: false, modulePreload: false } });
const assets = path.join(out, 'assets');
const files = fs.readdirSync(assets);
const js = fs.readFileSync(path.join(assets, files.find((f) => f.endsWith('.js'))), 'utf8');
const css = fs.readFileSync(path.join(assets, files.find((f) => f.endsWith('.css'))), 'utf8');
if (js.includes('</script')) throw new Error('bundle contains </script — cannot inline safely');
const src = fs.readFileSync('index.html', 'utf8');
const body = src.slice(src.indexOf('<body>') + 6, src.indexOf('</body>')).replace(/<script[^>]*src="[^"]*"[^>]*><\/script>/, '').trim();
const html = `<title>Dino Park Tycoon</title>
<meta name="theme-color" content="#7cc257">
<style>${css}</style>
${body}
<script type="module">${js}</script>
`;
fs.writeFileSync('dist-single/dino-park-tycoon.html', html);
fs.rmSync(out, { recursive: true });
console.log('dist-single/dino-park-tycoon.html', (html.length / 1024).toFixed(0), 'KB');
