# Permit To Work — Angular client

The frontend for the Permit To Work API. **Setup and run instructions live in the
[root README](../README.md)** — the client needs the API and the database running to do
anything, so there is no useful way to start here.

```bash
npm install
npm start     # http://localhost:4200, proxying /api to https://localhost:7188
npm test      # Vitest
npm run build # production build into dist/
```

Angular 22: standalone components, signals, zoneless change detection, and the built-in
control flow (`@if` / `@for`).

```
src/app/
├── core/        auth service and guards, typed API clients, shared models, settings
└── features/    login, employees, teams, permits, approvals, admin, settings
```

Routes are guarded twice — an auth guard for the token, and a role guard on the
administration screens. The proxy configuration is in `proxy.conf.json`; `secure: false`
is there so the dev server accepts the API's self-signed development certificate.
