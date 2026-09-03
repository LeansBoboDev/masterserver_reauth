# MasterServerReAuth

Vintage Story server mod that retries master server registration when the
initial registration attempt fails, instead of requiring a server restart.

## The bug

Vanilla registration logic lives in `Vintagestory.Server.ServerSystemHeartbeat`
(inside `VintagestoryLib.dll`):

- `SendHeartbeat()` only triggers a re-registration (`SendRegister()`) when the
  **heartbeat** request comes back with status `"invalid"` or `"timeout"`. The
  heartbeat request requires an existing token, i.e. the server must already be
  registered for this path to run at all.
- `SendRegister()` itself can also time out (e.g. `HttpClient.Timeout` of 10s
  elapsing). When that happens, its callback only special-cases the
  `"blacklisted"` and `"ok"` statuses — a `"timeout"` status falls through to
  the generic `else` branch, which just logs `"Could not register to master
  server..."` and does nothing else.
- Since the registration failed, `token` is never set. `SendHeartbeat()` no-ops
  whenever `token` is null/empty, so the heartbeat loop (which ticks every 2
  minutes) keeps running forever without ever attempting to register again.

Net effect: a single timed-out registration request permanently drops the
server off the public server list until it is restarted (or `AdvertiseServer`
is manually toggled off/on in the config).