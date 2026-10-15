using UnityEngine.Scripting.APIUpdating;
using UnityEngine;

namespace Zeraveene.BetterUGUI.Animation
{
    [System.Flags]
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.ProceduralUI.Animation", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public enum UIAnimationProperties { None = 0, Position = 1, Scale = 2, Rotation = 4, Alpha = 8 }

    // Components implement this interface once; starting an animation needs no delegate or closure.
    public interface IUIAnimationReceiver
    {
        void ApplyAnimation(float progress);
        void AnimationCompleted();
    }

    [MovedFrom(true, sourceNamespace: "MaxsusWorks.ProceduralUI.Animation", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public struct UIAnimation
    {
        public Behaviour Owner;
        public RectTransform Rect;
        public CanvasGroup Group;
        public UIAnimationProperties Properties;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public float Alpha, Duration;
        public UIEase Ease;
        public AnimationCurve Curve;
        public bool UnscaledTime;
        public IUIAnimationReceiver Receiver;
    }

    [MovedFrom(true, sourceNamespace: "MaxsusWorks.ProceduralUI.Animation", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public readonly struct UIAnimationHandle
    {
        internal readonly int Slot;
        internal readonly ulong Version;
        internal UIAnimationHandle(int slot, ulong version) { Slot = slot; Version = version; }
        public bool IsActive => UIAnimationRunner.IsActive(this);
        public void Cancel() => UIAnimationRunner.Cancel(this);
        public void Complete() => UIAnimationRunner.Complete(this);
    }
}
