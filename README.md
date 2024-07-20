# OutlineFx
[![Twitter](https://img.shields.io/badge/Follow-Twitter?logo=X&color=red)](https://x.com/NullTale)
[![Discord](https://img.shields.io/badge/Discord-Discord?logo=discord&color=white)](https://discord.gg/CkdQvtA5un)
[![Boosty](https://img.shields.io/badge/Support-Boosty?logo=boosty&color=white)](https://boosty.to/nulltale)


Simplified screen-space outline for Unity Urp.<br>
Supports transparency and does not require the use of special materials.<br>
Includes customization options such as:<br>
- Edge softness
- Shape fill
- Mask with its animation

![A](https://github.com/NullTale/OutlineFx/assets/1497430/d6367587-2203-47e7-8a13-6d16195f2adc)

The outline effect is achieved in the following way: selected objects are first rendered into a texture.<br>
Then, the edges of objects on this texture are drawn over the main texture.<br>
Optionally, the outline can be drawn in a separate texture and then used through a shader.<br>


# Usage
Unity PackageManager url
```
https://github.com/NullTale/OutlineFx.git
```
Basically, all you need to do is add `OutlineFxFeature` to the Urp Renderer<br>
and `OutlineFx` script to the object you want to outline.<br>

That's it, the outline settings are common for all objects,<br>
you can also customize the outline shape, fill and its texture.

![B](https://github.com/NullTale/OutlineFx/assets/1497430/2d05e249-61dd-40e8-af98-cc3a645495c8)
