using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zeraveene.BetterUGUI.Animation;
using UnityEngine.Scripting.APIUpdating;

namespace Zeraveene.SDK.UIEffect
{
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.SDK.UIEffect", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public class ButtonScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public float hoverScale = 1.1f;
        public float clickScale = 0.9f;
        public float tweenDuration = 0.1f;

        private Vector3 originalScale;
        private RectTransform rectTransform;
        private UIAnimationHandle currentTween;
        private bool hovering;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;
        }

        void OnDisable()
        {
            currentTween.Cancel();
            hovering = false;
            if (rectTransform != null) rectTransform.localScale = originalScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            ScaleTo(hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            ScaleTo(1f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ScaleTo(clickScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ScaleTo(hovering ? hoverScale : 1f);
        }

        private void ScaleTo(float scale)
        {
            currentTween.Cancel();
            currentTween = UIAnimationRunner.Play(new UIAnimation
            {
                Owner = this, Rect = rectTransform, Properties = UIAnimationProperties.Scale,
                Scale = originalScale * scale, Duration = tweenDuration, Ease = UIEase.OutQuad
            });
        }
    }

}
