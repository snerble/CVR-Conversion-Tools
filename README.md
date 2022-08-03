# CVR-Conversion-Tools
Collection of Unity editor tools I wrote to make life a little easier.

## CVR Contacts Setup
Utility for automatically setting up hand colliders and `CVRPointer` components on an avatar's fingertips. Intended to simulate the contacts system from
VRChat's Avatar Dynamics.

### Depends on
- [ChilloutVR SDK](https://documentation.abinteractive.net/cck/)

## CVR Conversion Tools
Utility for migrating VRChat animators to ChilloutVR. Changes the type of some parameters and then merges it with the default CVR AvatarAnimator.

### Depends on
- [ChilloutVR SDK](https://documentation.abinteractive.net/cck/)

## PhysBones to Dynamic Bones
Utility for converting PhysBones and PhysBone colliders to Dynamic Bones. Mostly reverse engineered from the DynamicBone to PhysBone migrator in the VRC SDK.

There's an additional feature that aims to reduce the stiffness of the converted bones by reducing the elasticity based on bone chain length. This is an attempt
to make longer bone chains appear heavier.

### Depends on
- VRChat Avatar 3.0 SDK
- [ChilloutVR SDK](https://documentation.abinteractive.net/cck/)
