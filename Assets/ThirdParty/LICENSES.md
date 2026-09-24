# Third-Party Asset Licences

## Kenney Blocky Characters

| Folder | Pack | Source | Models used |
|---|---|---|---|
| `Kenney_BlockyCharacters/` | Blocky Characters | https://kenney.nl/assets/blocky-characters | 4 of 18 |

Author: **Kenney** (www.kenney.nl)
Licence: **Creative Commons Zero (CC0 1.0)**, https://creativecommons.org/publicdomain/zero/1.0/

CC0 is a public domain dedication: no attribution is required and there are no restrictions on
use. Credit is given anyway. The pack's original unmodified `License.txt` is kept alongside the
models.

Only 4 of the 18 characters are committed, and only in FBX, since the pack ships the same
geometry in FBX, OBJ and GLB. `character-a` is the one the scene actually uses; the others are
kept so the player model can be swapped without re-downloading.

### A note on the rig

These characters are rigged as six separate parts (`root`, `leg-left`, `leg-right`, `torso`,
`arm-left`, `arm-right`, `head`) with each pivot already placed at its joint, but the pack ships
**no animation clips**. Rather than source clips from elsewhere, `CharacterAnimator.cs` drives
those transforms procedurally from the Rigidbody's measured velocity. See the README.

## Everything else

All scripts, materials, the scene and the course geometry are original work for this task.
No other third-party content is used, and the project depends on no packages beyond the Unity
modules included in a default 3D project.
