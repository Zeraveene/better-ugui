using UnityEngine;
using Zeraveene.BetterUGUI.Animation;
using Zeraveene.Shared.Inspector;
using UnityEngine.Scripting.APIUpdating;

namespace Zeraveene.Shared
{
    [CreateAssetMenu(fileName = "UITransitionPreset", menuName = "Summertime/UI/Transition Preset", order = 2)]
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.Shared", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public sealed class UITransitionPreset : ScriptableObject
    {
        [SerializeField] private bool enableFade = false;

        [SerializeField] private bool enableSlide = false;

        [SerializeField] private bool enableScale = false;

        [ShowIf(nameof(enableSlide))]
        [Tooltip("Direction to slide in from when showing")]
        [SerializeField] private UITransitionType slideInDirection = UITransitionType.Bottom;

        [ShowIf(nameof(enableSlide))]
        [Tooltip("Direction to slide out to when hiding")]
        [SerializeField] private UITransitionType slideOutDirection = UITransitionType.Top;

        [ShowIf(nameof(enableScale))]
        [SerializeField] private UIScaleMode scaleInMode = UIScaleMode.Shrink;

        [ShowIf(nameof(enableScale))]
        [SerializeField] private UIScaleMode scaleOutMode = UIScaleMode.Expand;

        [ShowIf(nameof(UseTransition))]
        [SerializeField] private float transitionDuration = 0.3f;

        [ShowIf(nameof(UseTransition))]
        [SerializeField] private UIEase transitionEase = UIEase.InOutQuad;

        [ShowIf(nameof(UseCustomTransitionEase))]
        [SerializeField] private bool useSeparateShowHideCurves;

        [ShowIf(nameof(UseSharedCustomTransitionEase))]
        [SerializeField] private AnimationCurve customTransitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [ShowIf(nameof(UseSeparateCustomTransitionEase))]
        [SerializeField] private AnimationCurve customShowEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [ShowIf(nameof(UseSeparateCustomTransitionEase))]
        [SerializeField] private AnimationCurve customHideEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private bool UseTransition => enableFade || enableSlide || enableScale;
        private bool UseCustomTransitionEase => UseTransition && transitionEase == UIEase.Custom;
        private bool UseSharedCustomTransitionEase => UseCustomTransitionEase && !useSeparateShowHideCurves;
        private bool UseSeparateCustomTransitionEase => UseCustomTransitionEase && useSeparateShowHideCurves;

        public bool EnableFade => enableFade;
        public bool EnableSlide => enableSlide;
        public bool EnableScale => enableScale;
        public UITransitionType SlideInDirection => slideInDirection;
        public UITransitionType SlideOutDirection => slideOutDirection;
        public UIScaleMode ScaleInMode => scaleInMode;
        public UIScaleMode ScaleOutMode => scaleOutMode;
        public float TransitionDuration => transitionDuration;
        public UIEase TransitionEase => transitionEase;
        public AnimationCurve CustomTransitionEase => customTransitionEase;
        public AnimationCurve CustomShowEase => customShowEase;
        public AnimationCurve CustomHideEase => customHideEase;
        public bool UseSeparateShowHideCurves => useSeparateShowHideCurves;
    }
}
