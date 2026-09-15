# Protocol fixtures

Captured byte fixtures now live under [`tools/re/fixtures/<build>/`](../../tools/re/fixtures/) (transport cipher, handshake KDF, `0x264` inbound wire, `0x120` outbound wire, keystream reuse), where the offline decoder and the `Gw2.Protocol.Tests` harness consume them. This directory is a placeholder for future Unity-side replay fixtures.

For each fixture record build, direction, connection role, capture layer, provenance, sanitization, expected decode and unknown fields. Label synthetic bytes explicitly. Keep original private captures in `captures/local`, and never call a native memory record a network packet fixture.
