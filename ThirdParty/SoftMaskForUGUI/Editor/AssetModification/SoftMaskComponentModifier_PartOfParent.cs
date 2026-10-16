using Coffee.UISoftMaskInternal;
using Coffee.UISoftMaskInternal.AssetModification;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

#pragma warning disable CS0612, CS0618 // Type or member is obsolete

namespace Coffee.UISoftMask
{
    internal class SoftMaskComponentModifier_PartOfParent : ComponentModifier<SoftMask>
    {
        protected override bool ModifyComponent(SoftMask softMask, bool dryRun)
        {
            if (!softMask.partOfParent) return false;

            if (!dryRun)
            {
                var shape = softMask.GetOrAddComponent<MaskingShape>();
                shape.softnessRange = softMask.softnessRange;
                shape.alphaHitTest = softMask.alphaHitTest;
                shape.showMaskGraphic = softMask.showMaskGraphic;

                Object.DestroyImmediate(softMask);
                EditorUtility.SetDirty(shape.gameObject);
            }

            return true;
        }

        public override string Report()
        {
            return "  -> SoftMask.partOfParent is obsolete. Use MaskingShape component instead.\n";
        }
    }
}
