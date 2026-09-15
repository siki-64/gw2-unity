# Auth providers (Steam / ANet Portal / Epic)

**Build:** `205.780` (Gw2-64.exe, image SHA-256
`D2AE84876A0B93277FCCB368969046B848BB0403FD09DB420389C813D2459B23`). Addresses are build-local
coordinates; do not carry them to another build.

**Status:** **static only** — recovered from strings, tables and decompiled call sites. No capture,
no wire fixture; the portal's HTTP exchange was not traced.

**Scope:** this is the **launcher / portal auth layer**, *not* the recovered datacenter game
handshake. The `CLI2GAME` connection ([session-state.md](session-state.md), the DH + RC4 handshake)
carries **no** provider field; the storefront is resolved earlier and only a credential/session token
crosses into the game connection. See the "Where the provider is not" section.

## Launcher login form (`LauncherCoherent.cpp`)

`FUN_1413d3ed0` reads a `provider` property from the login form
(`(*(*form + 0x48))(form, L"provider", &out)`) and branches:

| `provider` value(s) | Path | Credential material |
| --- | --- | --- |
| `Steam`, `steam` | Steam SDK (`steam_api64.dll`) | Steam auth ticket via `FUN_140e2c7a0` / `FUN_140e2c990`, then hex-encoded (`%02x`) and passed as the credential |
| `Epic`, `epic` | EOS SDK (`EOS_Auth_Login`, `EOS_Auth_CopyUserAuthToken`) | EOS auth token; env `EpicPortal`, `epicapp`, `epicenv`, `epicusername`, `epicuserid`, `epicsandboxid`, `epicdeploymentid`, `exchangecode` |
| anything else | default account path | form `password`, encrypted with `FUN_141578ee0` / `FUN_141578fa0` |

The strings compared are localized (`FUN_1410e4d10(0x8d)` = `"Steam"`, similarly `"Epic"`), so the
comparison is case-insensitive against the localized names. Relevant error strings:
`"Epic login failed error: %d"`, `"Epic login failed to copy auth token, error: %d"`.

## Portal auth exchange (`PortalCli.cpp`, `Cli2PortalAuth`)

`FUN_140ff3190` registers the `"Cli2PortalAuth"` socket (`cligate`); requests/responses are JSON
property bags (`FUN_140ff7c00` builds a request, `FUN_140ff56b0` handles a response). Fields
observed:

| Field | Meaning |
| --- | --- |
| `Provider` | **the provider string** (written only when non-empty). The literal `"Portal"` is compared in the password path (`FUN_1409badf0(..., "Portal", -1)`), so `Portal` = ArenaNet |
| `LoginName`, `UserId`, `ResumeToken` | account identity |
| `Scopes` | e.g. `_self full_economy` |
| `AuthType` | 2FA method (see below) |
| `PasswordToken`, `PasswordTokenVersion` | password/token scheme |
| `OldPasswordHash`, `OldPasswordType`, `Verifier`, `PasswordType`, `Gw1PasswordHash` | password material |
| `RequestId`, `KeyData`, `AuthPromptHeader`, `AuthPromptInput`, `EmailVerified` | prompt/flow control |

So the provider **is** explicit in the auth exchange (as `Provider`), distinct from the auth method.

## `AuthType` is the 2FA method, not the provider

Name→int table at `14210b5c0`, read by `FUN_140ffb880` (default `8` when unknown):

| Value | Name | | Value | Name |
| --- | --- | --- | --- | --- |
| `0` | `None` | | `5` | `EmailCode` |
| `1` | `SMS` | | `6` | `Totp` |
| `2` | `Smartphone` | | `7` | `MultiInput` |
| `3` | `Vasco` | | `8` | (default / unknown) |
| `4` | `Email` | | | |

## `PasswordType` / `PasswordTokenVersion`

int→name table at `14210c780`, read by `FUN_140fffd50` / `FUN_140fffd80` (default `Invalid`):

| Value | Name |
| --- | --- |
| `0x00` | `srp` |
| `0x01` | `srp2` |
| `0x02` | `gw1` |
| `0xff` | `ssoPasswordToken` |

## Where the provider is *not*

The `CLI2GAME` datacenter handshake carries no provider: it is a Diffie-Hellman exchange followed by
an RC4-variant transport cipher ([handshake-key-derivation.md](handshake-key-derivation.md),
[transport-cipher.md](transport-cipher.md), [session-state.md](session-state.md)). Provider selection
and the credential exchange happen in the launcher/portal layer before the game connection opens.

## Open

- The portal's HTTP/JSON exchange itself was not traced (endpoint, TLS, token format).
- No capture distinguishes the three providers end to end.
- The exact `provider` literal set is inferred from the two comparisons plus the `"Portal"` literal;
  other providers (if any) would take the default path.
