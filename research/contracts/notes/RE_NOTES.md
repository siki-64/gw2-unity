# Reverse-engineering negative findings

**Confirmed build(s):** per finding; see each finding's evidence line.<br>
**Status:** durable negative findings, not current-build positive contracts.<br>
**Unresolved:** applicability to a current build unless independently revalidated.

This file preserves **durable negative findings**: disproven interpretations, ruled-out mappings, and failed hypotheses that are useful enough to prevent the same reverse-engineering mistake from being repeated.

It is not a chronological work log and it is not the canonical home for confirmed behavior.

Use the repository layers as follows:

- provisional observations, hypotheses, and incomplete leads are kept as scratch work and are not part of these findings;
- `research/contracts/notes/<area>/...` — current confirmed behavior and subsystem-specific reconstruction;
- this file — durable negative findings whose absence would make a future incorrect interpretation likely to recur.

## What belongs here

Record a negative finding when it rules out a plausible interpretation that is likely to be rediscovered, for example:

- a field, bit, vtable slot, function, or global was tested and does **not** have the suspected role;
- two structures or id domains were shown not to be interchangeable;
- a visually convincing correlation was disproved by controlled live testing;
- a patch site changed the observed behavior but did not establish the broader semantic interpretation originally attached to it;
- a candidate locator or reconstruction path was rejected for a durable structural reason.

Do not duplicate every failed experiment. Short-lived exploration remains in scratch notes or Git history.

## Entry format

Keep entries concise and evidence-oriented:

```text
### <subject>

Hypothesis: <what was suspected>
Result: <what disproved or bounded it>
Evidence: <build, static path, live test, or other decisive observation>
Implication: <what future reconstruction must not assume>
```

When a negative finding is already explained clearly in the authoritative subsystem document, prefer linking to that document instead of copying the full reconstruction here.

## Current principle

A disproved semantic name should be removed from the canonical layout or behavioral documentation. Preserve it here only when the negative result remains useful to future reverse engineering.

### ChCliHealth larger-offset field attribution

Hypothesis: health-like triplets at `+0x120..+0x128` and `+0x274..+0x27C` belonged to the
character-owned `ChCliHealth` object.  
Result: the native `ChCliHealth` allocation/destruction boundary is exactly `0x88` bytes, so those
offsets cannot belong to that object.  
Evidence: build `205.780` constructor/destructor and allocation-size reconstruction.  
Implication: keep those larger-offset fields unassigned until their actual owner is independently
recovered; do not reattach them to `ChCliHealth`.
