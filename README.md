# OutlineFx
⚪ Dev by NullTale<br>
[![Twitter](https://img.shields.io/badge/Twitter-Twitter?logo=X&color=red)](https://x.com/NullTale)
[![Itch](https://img.shields.io/badge/Web-Itch?logo=Itch.io&color=white)](https://nulltale.itch.io)
[![Tg](https://img.shields.io/badge/Tg-Telegram?logo=telegram&color=white)](https://t.me/nulltalescape)

Simplified screen-space outline for Unity Urp.<br>
Supports transparency and does not require the use of special materials.<br>
Includes customization options such as:<br>
- Edge softness
- Shape fill
- Mask with its animation

Support developement https://boosty.to/nulltale/single-payment/donation/639461/target?share=target_link

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

The texture source can also be output to a global texture <br>
and used manually via a shader or processed with [VolFx](https://github.com/NullTale/VolFx)

![ _cover](https://github.com/user-attachments/assets/5a95752e-e751-4377-b102-898415db8362)<br>
> example demonstrates the VolFx flow effect applied to the outline source to simulate a fire effect.
