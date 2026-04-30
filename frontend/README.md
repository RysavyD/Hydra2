# Hydra2 frontend (TypeScript)

Zdrojový TypeScript pro klientskou část `Hydra2.Web`. Build produkuje minifikované JS soubory do `../src/Hydra2.Web/wwwroot/js/`.

## Setup

```bash
cd frontend
npm install
```

## Build

```bash
npm run build      # jednorázový minified build
npm run watch      # rebuild při uložení (dev)
npm run typecheck  # TypeScript validation bez emitu
```

## Co se buildí

- `src/graf.ts` → `../src/Hydra2.Web/wwwroot/js/graf.min.js` (+ `.map`)

`graf.min.js` je commitnutý v gitu (Forpsi shared hosting nemá Node) — po editaci `src/graf.ts` spusť `npm run build` a commit i nový `graf.min.js`.
