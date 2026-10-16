using Coffee.UISoftMaskInternal;
using UnityEngine.UI;

namespace Coffee.UISoftMask
{
    public interface ISoftMasking : IMeshModifier
    {
        bool enabled { get; }

        MaskingShape.MaskingMethod maskingMethod { get; }

        /// <summary>
        /// Show the graphic that is associated with the Mask render area.
        /// </summary>
        bool showMaskGraphic { get; }

        /// <summary>
        /// The transparent part of the mask cannot be clicked.
        /// This can be achieved by enabling Read/Write enabled in the Texture Import Settings for the texture.
        /// <para></para>
        /// NOTE: Enable this only if necessary, as it will require more graphics memory and processing time.
        /// </summary>
        bool alphaHitTest { get; }

        /// <summary>
        /// Threshold for anti-alias masking.
        /// The smaller this value, the less jagged it is.
        /// </summary>
        float antiAliasingThreshold { get; }

        /// <summary>
        /// The minimum and maximum alpha values used for soft masking.
        /// The larger the gap between these values, the stronger the softness effect.
        /// </summary>
        MinMax01 softnessRange { get; }

        /// <summary>
        /// Method to determine whether this masking shape should be a raycast target.
        /// </summary>
        MaskingShape.RaycastMethod raycastMethod { get; }
    }
}
