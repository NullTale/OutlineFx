using UnityEngine;

//  OutlineFx © NullTale - https://x.com/NullTale/
namespace OutlineFx
{
    [DefaultExecutionOrder(10000)]
    public class OutlineBlocker : Outline
    {
        private static Color s_color = new Color(0, 0, 0, 7f / 255f);

        public override Color Color
        {
            get => new Color(0, 0, 0, 14f / 255f);
            set
            {
                // pass
            }
        }
    }
}