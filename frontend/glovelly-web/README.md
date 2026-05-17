# Glovelly Web

This is the React frontend for Glovelly. It is built with Vite and is served by
the ASP.NET Core backend in deployed environments.

## Local Development

From the repository root, run the full local stack:

```bash
./run-dev.sh
```

This starts the frontend on `http://localhost:5173` and the backend API on
`http://localhost:5153`.

To run only the frontend development server:

```bash
npm --prefix frontend/glovelly-web run dev
```

## Checks

Run these from the repository root after frontend changes:

```bash
npm --prefix frontend/glovelly-web run lint
npm --prefix frontend/glovelly-web run build
```

## Project Notes

- API requests should use the helpers in `src/api.ts` so session expiry and
  problem details handling remain consistent.
- Shared API/domain shapes live in `src/types.ts`.
- Stateful workspace behavior belongs in hooks under `src/hooks`; components in
  `src/components` should mostly render state and call hook-provided handlers.
