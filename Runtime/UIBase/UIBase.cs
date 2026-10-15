using UnityEngine;
using Zeraveene.BetterUGUI.Animation;
using Zeraveene.Shared.Inspector;
using System;
using UnityEngine.Scripting.APIUpdating;
// using Zeraveene.Events;

namespace Zeraveene.Shared
{
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.Shared", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public enum UITransitionSource
    {
        Local,
        Preset
    }

    // Ensure RectTransform exists
    [RequireComponent(typeof(RectTransform))]
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.Shared", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public class UIBase : MonoBehaviour, IUIAnimationReceiver
    {
        #region Inspector
        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [SerializeField] private bool deactivateOnInitialize;

        [PropertyOrder(999), FoldoutGroup("UI Transition Settings")]
        [SerializeField] private UITransitionSource transitionSource = UITransitionSource.Local;

        [PropertyOrder(999), FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(IsPresetSource)), InlineEditor]
        [InfoBox("No preset assigned. Using local transition settings.", InfoMessageType.Warning, nameof(IsTransitionPresetMissing))]
        [SerializeField] private UITransitionPreset transitionPreset;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings", expanded: true)]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private bool enableFade = false;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private bool enableSlide = false;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private bool enableScale = false;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(enableSlide))]
        [Tooltip("Direction to slide in from when showing")]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private UITransitionType slideInDirection = UITransitionType.Bottom;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(enableSlide))]
        [Tooltip("Direction to slide out to when hiding")]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private UITransitionType slideOutDirection = UITransitionType.Top;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(enableScale))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private UIScaleMode scaleInMode = UIScaleMode.Shrink;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(enableScale))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private UIScaleMode scaleOutMode = UIScaleMode.Expand;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(UseTransition))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private float transitionDuration = 0.3f;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(UseTransition))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private UIEase transitionEase = UIEase.InOutQuad;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(UseCustomTransitionEase))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private bool useSeparateShowHideCurves;

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(UseSharedCustomTransitionEase))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private AnimationCurve customTransitionEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(UseSeparateCustomTransitionEase))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private AnimationCurve customShowEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [PropertyOrder(999)]
        [FoldoutGroup("UI Transition Settings")]
        [ShowIf(nameof(UseSeparateCustomTransitionEase))]
        [HideIf(nameof(UsesTransitionPreset))]
        [SerializeField] private AnimationCurve customHideEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [PropertyOrder(1000)]
        [FoldoutGroup("UI Sound Settings")]
        [SerializeField] private bool emitAudioEvents = false;
        #endregion

        #region Runtime Data
        private bool isShown;
        private bool UseTransition => enableFade || enableSlide || enableScale;
        private bool UseCustomTransitionEase => UseTransition && transitionEase == UIEase.Custom;
        private bool UseSharedCustomTransitionEase => UseCustomTransitionEase && !useSeparateShowHideCurves;
        private bool UseSeparateCustomTransitionEase => UseCustomTransitionEase && useSeparateShowHideCurves;
        protected CanvasGroup canvasGroup;
        protected RectTransform rectTransform;
        protected Vector2 originalAnchoredPosition;
        private UIAnimationHandle activeAnimation;
        private uint animationRequest;
        protected bool isInitialized;
        #endregion

        #region Callback
        public Action OnStartShow;
        public Action OnStartHide;
        public Action OnCompleteShow;
        public Action OnCompleteHide;
        #endregion

        #region Properties
        public UITransitionSource TransitionSource => transitionSource;
        public UITransitionPreset TransitionPreset => transitionPreset;
        private bool IsPresetSource => transitionSource == UITransitionSource.Preset;
        private bool UsesTransitionPreset => IsPresetSource && transitionPreset != null;
        private bool IsTransitionPresetMissing => IsPresetSource && transitionPreset == null;

        // Keep local fields at their original serialized paths to preserve prefab overrides.
        private bool ResolvedEnableFade => UsesTransitionPreset ? transitionPreset.EnableFade : enableFade;
        private bool ResolvedEnableSlide => UsesTransitionPreset ? transitionPreset.EnableSlide : enableSlide;
        private bool ResolvedEnableScale => UsesTransitionPreset ? transitionPreset.EnableScale : enableScale;
        private UITransitionType ResolvedSlideInDirection => UsesTransitionPreset ? transitionPreset.SlideInDirection : slideInDirection;
        private UITransitionType ResolvedSlideOutDirection => UsesTransitionPreset ? transitionPreset.SlideOutDirection : slideOutDirection;
        private UIScaleMode ResolvedScaleInMode => UsesTransitionPreset ? transitionPreset.ScaleInMode : scaleInMode;
        private UIScaleMode ResolvedScaleOutMode => UsesTransitionPreset ? transitionPreset.ScaleOutMode : scaleOutMode;
        private float ResolvedTransitionDuration => UsesTransitionPreset ? transitionPreset.TransitionDuration : transitionDuration;
        private UIEase ResolvedTransitionEase => UsesTransitionPreset ? transitionPreset.TransitionEase : transitionEase;
        private AnimationCurve ResolvedCustomTransitionEase => UsesTransitionPreset ? transitionPreset.CustomTransitionEase : customTransitionEase;
        private AnimationCurve ResolvedCustomShowEase => UsesTransitionPreset ? transitionPreset.CustomShowEase : customShowEase;
        private AnimationCurve ResolvedCustomHideEase => UsesTransitionPreset ? transitionPreset.CustomHideEase : customHideEase;
        private bool ResolvedUseSeparateShowHideCurves => UsesTransitionPreset ? transitionPreset.UseSeparateShowHideCurves : useSeparateShowHideCurves;

        public void SetTransitionPreset(UITransitionPreset preset)
        {
            transitionPreset = preset;
            transitionSource = UITransitionSource.Preset;
        }

        public void UseLocalTransitionSettings()
        {
            transitionSource = UITransitionSource.Local;
        }

        public float TransitionDuration => ResolvedTransitionDuration;
        public bool EmitAudioEvents => emitAudioEvents;
        #endregion

#if UNITY_EDITOR
        [PropertyOrder(999), FoldoutGroup("UI Transition Settings")]
        [Button("Edit From This Preset"), ShowIf(nameof(IsPresetSource))]
        [EnableIf(nameof(UsesTransitionPreset))]
        private void EditFromTransitionPreset()
        {
            if (!UsesTransitionPreset) return;

            UnityEditor.Undo.RecordObject(this, "Edit UI Transition From Preset");
            CopyTransitionProperties(transitionPreset, this);
            UseLocalTransitionSettings();
            RecordTransitionSettingsChange();
        }

        [PropertyOrder(999), FoldoutGroup("UI Transition Settings")]
        [Button("Create Preset From Active Settings")]
        private void CreateTransitionPreset()
        {
            string path = UnityEditor.EditorUtility.SaveFilePanelInProject(
                "Create UI Transition Preset", gameObject.name + "Transition", "asset",
                "Choose where to save the reusable transition preset.",
                "Assets");
            if (string.IsNullOrEmpty(path)) return;

            var preset = ScriptableObject.CreateInstance<UITransitionPreset>();
            CopyTransitionProperties(UsesTransitionPreset ? (UnityEngine.Object)transitionPreset : this, preset);
            UnityEditor.AssetDatabase.CreateAsset(preset, UnityEditor.AssetDatabase.GenerateUniqueAssetPath(path));
            UnityEditor.Undo.RegisterCreatedObjectUndo(preset, "Create UI Transition Preset");
            UnityEditor.AssetDatabase.SaveAssets();

            UnityEditor.Undo.RecordObject(this, "Assign UI Transition Preset");
            SetTransitionPreset(preset);
            RecordTransitionSettingsChange();
            UnityEditor.Selection.activeObject = preset;
        }

        private void RecordTransitionSettingsChange()
        {
            UnityEditor.EditorUtility.SetDirty(this);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        }

        private static void CopyTransitionProperties(UnityEngine.Object source, UnityEngine.Object destination)
        {
            // The preset defines the shared fields. Local serialization paths stay intact,
            // and SerializedProperty copies curves by value rather than sharing references.
            var sourceObject = new UnityEditor.SerializedObject(source);
            var destinationObject = new UnityEditor.SerializedObject(destination);
            var presetObject = source is UITransitionPreset ? sourceObject : destinationObject;
            var property = presetObject.GetIterator();
            bool enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script") continue;
                var sourceProperty = sourceObject.FindProperty(property.propertyPath);
                if (sourceProperty != null && destinationObject.FindProperty(property.propertyPath) != null)
                    destinationObject.CopyFromSerializedProperty(sourceProperty);
            }
            destinationObject.ApplyModifiedPropertiesWithoutUndo();
        }
#endif

        private void EnsureInitialized()
        {
            if (!isInitialized) Initialize(true);
        }

        #region Initialization
        public void Initialize(bool force = false)
        {
            if (isInitialized && !force) return;
            HandleInitialize();
        }

        protected virtual void HandleInitialize()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            rectTransform = GetComponent<RectTransform>();
            originalAnchoredPosition = rectTransform.anchoredPosition;

            if (deactivateOnInitialize)
            {
                gameObject.SetActive(false);
            }

            isInitialized = true;
        }
        #endregion

        #region Public Methods
        public virtual void Show(bool force = true)
        {
            EnsureInitialized();
            if (isShown && !force) return;
            uint request = ++animationRequest;
            activeAnimation.Cancel();
            isShown = true;
            OnStartShow?.Invoke();
            if (request != animationRequest || this == null) return;
            gameObject.SetActive(true);
            if (!isActiveAndEnabled) return;
            rectTransform.anchoredPosition = originalAnchoredPosition +
                (ResolvedEnableSlide ? GetSlideOffset(ResolvedSlideInDirection) : Vector2.zero);
            canvasGroup.alpha = ResolvedEnableFade ? 0f : 1f;
            rectTransform.localScale = ResolvedEnableScale ? ScaleEndpoint(ResolvedScaleInMode) : Vector3.one;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = true;
            StartTransition(true, originalAnchoredPosition, Vector3.one);
        }

        public virtual void Hide(bool force = true)
        {
            EnsureInitialized();
            if (!isShown && !force) return;
            uint request = ++animationRequest;
            activeAnimation.Cancel();
            isShown = false;
            OnStartHide?.Invoke();
            if (request != animationRequest || this == null) return;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
            if (!isActiveAndEnabled)
            {
                FinishHide();
                return;
            }
            StartTransition(false,
                originalAnchoredPosition + (ResolvedEnableSlide ? GetSlideOffset(ResolvedSlideOutDirection) : Vector2.zero),
                ScaleEndpoint(ResolvedScaleOutMode));
        }
        #endregion

        #region Private Methods
        private static Vector3 ScaleEndpoint(UIScaleMode mode) => mode switch
        {
            UIScaleMode.Shrink => Vector3.one * 3f,
            UIScaleMode.Expand => Vector3.zero,
            _ => Vector3.one
        };

        private void StartTransition(bool showing, Vector2 position, Vector3 scale)
        {
            UIAnimationProperties properties = UIAnimationProperties.None;
            if (ResolvedEnableSlide) properties |= UIAnimationProperties.Position;
            if (ResolvedEnableScale) properties |= UIAnimationProperties.Scale;
            if (ResolvedEnableFade) properties |= UIAnimationProperties.Alpha;
            uint request = animationRequest;
            var handle = UIAnimationRunner.Play(new UIAnimation
            {
                Owner = this, Rect = rectTransform, Group = canvasGroup,
                Properties = properties,
                Position = new Vector3(position.x, position.y, rectTransform.anchoredPosition3D.z),
                Scale = scale, Alpha = showing ? 1f : 0f,
                Duration = properties == UIAnimationProperties.None ? 0f : ResolvedTransitionDuration,
                Ease = ResolvedTransitionEase,
                Curve = ResolvedUseSeparateShowHideCurves
                    ? (showing ? ResolvedCustomShowEase : ResolvedCustomHideEase) : ResolvedCustomTransitionEase,
                Receiver = this
            });
            if (request == animationRequest) activeAnimation = handle;
            else handle.Cancel();
        }

        void IUIAnimationReceiver.ApplyAnimation(float progress) { }

        void IUIAnimationReceiver.AnimationCompleted()
        {
            activeAnimation = default;
            if (isShown) OnCompleteShow?.Invoke();
            else FinishHide();
        }

        private void FinishHide()
        {
            canvasGroup.alpha = 0f;
            rectTransform.anchoredPosition = originalAnchoredPosition;
            rectTransform.localScale = Vector3.one;
            gameObject.SetActive(false);
            OnCompleteHide?.Invoke();
        }

        protected virtual void OnDisable()
        {
            ++animationRequest;
            activeAnimation.Cancel();
            activeAnimation = default;
        }

        private Vector2 GetSlideOffset(UITransitionType direction)
        {
            Vector2 size = rectTransform.rect.size;
            Vector2 scaledSize = new Vector2(size.x * rectTransform.lossyScale.x, size.y * rectTransform.lossyScale.y);

            return direction switch
            {
                UITransitionType.Top => Vector2.up * scaledSize.y,
                UITransitionType.Bottom => Vector2.down * scaledSize.y,
                UITransitionType.Left => Vector2.left * scaledSize.x,
                UITransitionType.Right => Vector2.right * scaledSize.x,
                _ => Vector2.zero,
            };
        }

        protected void AnimateSlideOut(Vector2 direction)
        {
            EnsureInitialized();
            ++animationRequest;
            activeAnimation.Cancel();
            isShown = false;
            canvasGroup.interactable = canvasGroup.blocksRaycasts = false;
            Vector2 destination = originalAnchoredPosition + GetSlideOffset(direction);
            activeAnimation = UIAnimationRunner.Play(new UIAnimation
            {
                Owner = this, Rect = rectTransform, Properties = UIAnimationProperties.Position,
                Position = new Vector3(destination.x, destination.y, rectTransform.anchoredPosition3D.z),
                Duration = ResolvedTransitionDuration, Ease = ResolvedTransitionEase,
                Curve = ResolvedUseSeparateShowHideCurves ? ResolvedCustomHideEase : ResolvedCustomTransitionEase,
                Receiver = this
            });
        }

        private Vector2 GetSlideOffset(Vector2 direction)
        {
            Vector2 size = rectTransform.rect.size;
            Vector2 scaledSize = new Vector2(size.x * rectTransform.lossyScale.x, size.y * rectTransform.lossyScale.y);
            return new Vector2(direction.x * scaledSize.x, direction.y * scaledSize.y);
        }
        #endregion

        #region Debug
        [PropertyOrder(1000)]
        [Button, FoldoutGroup("Debug UI Transition")]
        private void DebugInitialize()
        {
            HandleInitialize();
            Debug.Log($"{gameObject.name} initialized with transition settings: Fade={ResolvedEnableFade}, Slide={ResolvedEnableSlide}, Scale={ResolvedEnableScale}, In={ResolvedSlideInDirection}, Out={ResolvedSlideOutDirection}, Duration={ResolvedTransitionDuration}s, UIEase={ResolvedTransitionEase}");
        }

        [PropertyOrder(1000)]
        [Button, FoldoutGroup("Debug UI Transition")]
        private void DebugShow()
        {
            Show();
            Debug.Log($"{gameObject.name} is shown with {ResolvedSlideInDirection} transition.");
        }

        [PropertyOrder(1000)]
        [Button, FoldoutGroup("Debug UI Transition")]
        private void DebugHide()
        {
            Hide();
            Debug.Log($"{gameObject.name} is hidden with {ResolvedSlideOutDirection} transition.");
        }
        #endregion
    }
}
