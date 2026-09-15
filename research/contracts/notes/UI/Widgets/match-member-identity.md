# Match member identity

Native match-channel member identity: how the current incoming match channel resolves
member account identities, and the offsets that expose them.

The reader consumes only the current incoming match channel's members, including names
cached by friends or earlier matches. There is no general-registry fallback. The channel
is re-read on a 250 ms cadence and its members are discarded when the queue ends. A member
whose name has not resolved retains its match ID and is reported as pending.

## Static contract (build 207.032)

Recovered in Ghidra on 2026-09-06:

```text
ContextCollection +0x218       PexCliContext*
PexCliContext +0xD0            PexCliMatch*
PexCliMatch +0x150             PlCliChannel*
PlCliChannel +0xCA0            channel type (must equal 3: match)
PlCliChannel +0xAA8            member pointer array
PlCliChannel +0xAB4            member count
member +0x78                  PlCliUser*
PlCliUser +0x18                16-byte portal identity
PlCliUser +0x280               inline UTF-16 account name (0x48 chars)
```

Evidence:

- `0x141448350` asserts `channel->GetType() == PORTAL_CHANNEL_TYPE_MATCH`
  (type 3) before creating the match; `0x14144E940` stores that channel at +0x150.
- `0x140273810` reads the channel type at +0xCA0.
- `0x1402733E0` traverses +0xAA8 with count +0xAB4, dereferences each entry's
  +0x78 user, and compares the users' complete IDs through vtable slot +0x130.
- `0x14026F7B0` implements that ID getter as this +0x18;
  `0x14026F7C0` returns the account name at this +0x280.

The earlier +0x10 identity offset was incorrect: it included a secondary vtable
pointer and only half the ID. The previous claim that the repeated prefix was an
identity namespace is superseded by the constructor/getter evidence and a live
user-object read confirming a module vtable at +0x10.

The reader bounds counts to 256, validates channel type and user vtables, excludes
duplicate/zero IDs, and discards failed reads or changed owner/array pointers.
Each returned row is classified as a match member.

## Validation

Synthetic-memory tests cover cached names, ID offset, duplicate members, removed
members, invalid pointers/counts, and non-match channels. Consumption tests cover
cached friends and earlier-pop identities without accepting unrelated identities.

Live inspection confirmed the portal-user ID offset. The match pointer was null
during implementation; a real incoming queue pop remains required to validate
the complete channel traversal and visible names. No native calls are invoked by
the reader.
