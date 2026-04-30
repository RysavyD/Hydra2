// Build script for Hydra2 client-side TypeScript bundles.
// Outputs to ../src/Hydra2.Web/wwwroot/js/.
import * as esbuild from "esbuild";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const __dirname = dirname(fileURLToPath(import.meta.url));
const outDir = resolve(__dirname, "../src/Hydra2.Web/wwwroot/js");

const watch = process.argv.includes("--watch");

/** @type {import('esbuild').BuildOptions} */
const options = {
    entryPoints: [resolve(__dirname, "src/graf.ts")],
    outfile: resolve(outDir, "graf.min.js"),
    bundle: true,
    minify: true,
    sourcemap: true,
    target: ["es2018"],
    format: "iife",
    logLevel: "info",
    legalComments: "none",
};

if (watch) {
    const ctx = await esbuild.context(options);
    await ctx.watch();
    console.log(`watching ${options.entryPoints[0]} -> ${options.outfile}`);
} else {
    const result = await esbuild.build(options);
    if (result.errors.length > 0) process.exit(1);
}
