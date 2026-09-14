# Havok physics objects

**Confirmed build(s):** unknown; evidence is from independently live-used character/gadget collision paths from July 2026, correlated with existing `AgChar`, `CoChar`, and `CoKeyFramed` layouts.<br>
**Status:** partial physics bridge and common Havok object fields recovered.<br>
**Unresolved:** complete Havok class sizes/inheritance, object ownership, motion layout, and stability across Havok/client revisions.

## Character physics bridge

`CoChar` exposes separate player and NPC physics paths:

- `+0x60 -> HkpRigidBody*` on the player path;
- `+0x88 -> CoCharSimpleCliWrapper*`;
- `+0x100 -> HkpSimpleShapePhantom*` on the player path;
- `+0x170 -> HkpBoxShape*` on the NPC path.

The intermediate `CoCharSimpleCliWrapper` contains `+0x68 HkpCharacterProxy*`, `+0x78 HkpSimpleShapePhantom*`, `+0xB8` alternate position 1, `+0xE8 HkpBoxShape*` on the NPC path, and `+0x118` alternate position 2.

Live consumers report `+0xB8` tracking the primary `CoChar +0x30` visual position closely, while `+0x118` can lag and should not be preferred for frame-accurate rendering. Player/NPC fields must remain separate.

`CoKeyFramed +0x60` is likewise typed as `HkpRigidBody*` for gadget/keyframed physics.

## Rigid body and common shape header

`HkpRigidBody` partially exposes `+0x10 HkpWorld*`, `+0x20 HkpShape*`, `+0x4C` wrapper shape type, and `+0x150` motion pointer. The shape itself carries a primitive `HkcdShapeType` at `+0x10`.

Observed primitive ids are `0x01 Cylinder`, `0x03 Box`, `0x08 List`, `0x09 MOPP`, and `0x0D ExtendedMesh`. The rigid-body wrapper classifier and primitive shape type are separate fields and must not be conflated.

## Shapes

`HkpBoxShape` has collision radius at `+0x20` and a `Vector4` half-extent at `+0x30`. NPC observations indicate width/depth can be capsule-like collision radii while height remains useful, so these values are not universal visual bounds.

`HkpCylinderShape` has radius at `+0x28` and half-height at `+0x2C`.

`HkpMoppBvTreeShape` has compressed/code data at `+0x28` and child `HkpShape*` at `+0x58`.

`HkpExtendedMeshShape` has cached AABB half-extents at `+0xC0`.

`HkpListShape` has bounding-box half-extents at `+0x50` and an alternate/backup height-half value at `+0x68`.

## Character controller and phantom

`HkpCharacterProxy +0x28` is the ground-state code: `0 InAir`, `1 OnGround`, `2 HillyGround`.

`HkpSimpleShapePhantom` exposes collision offset X at `+0x110` and physics position at `+0x120`. The phantom position tracks player movement closely and provides an independent physics-space position source.

## Physics world boundary

The partial chain is `HkpRigidBody +0x10 -> HkpWorld +0x188 -> HkpBroadPhaseBorder +0x00 -> six phantom pointers`.

`HkpAabbPhantom` exposes `Vector4 AabbMin` at `+0xF0` and `Vector4 AabbMax` at `+0x100`. This gives a direct native physics representation for the world/broadphase boundary, separate from the existing object-clipping patch site.

## Scope

These layouts stay internal to `Gw2.Native`; do not expose Havok pointers through the stable module API. Immediate RE uses are collision-dimension validation, visual-vs-physics position comparison, ground-state recovery, and connecting object-clipping research to concrete physics objects.

Do not build mutation features on these layouts until ownership, synchronization, and physics-thread rules are understood.
