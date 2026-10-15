using UnityEngine;
using System;
using System.Collections.Generic;
using Zeraveene.BetterUGUI.Animation;
using Zeraveene.Shared.Inspector;
using UnityEngine.Scripting.APIUpdating;

namespace Zeraveene.Shared
{
    [RequireComponent(typeof(RectTransform))]
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.Shared", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public class UIViewSequence : MonoBehaviour, IUIAnimationReceiver
    {
        [Serializable]
        public class ViewTransform
        {
            public Vector3 position;
            public Vector3 rotation;
            public Vector3 scale;
            public float duration = 1f;
        }

        [BoxGroup("View Settings")] public List<ViewTransform> views = new List<ViewTransform>();
        [BoxGroup("View Settings")] public float backToStartDuration = 0.5f;
        [BoxGroup("Live State"), ReadOnly] public Vector3 startPosition;
        [BoxGroup("Live State"), ReadOnly] public Vector3 startRotation;
        [BoxGroup("Live State"), ReadOnly] public Vector3 startScale;
        private RectTransform rect;
        private UIAnimationHandle activeAnimation;
        private struct Frame
        {
            internal Vector3 FromPosition, Position, FromScale, Scale;
            internal Quaternion FromRotation, Rotation;
            internal float Start, Duration;
        }
        private Frame[] frames = Array.Empty<Frame>();
        private int frameCount;
        private float totalDuration;

        private void Awake() => rect = GetComponent<RectTransform>();
        private void OnDisable() => activeAnimation.Cancel();

        [Button("Add Current View")]
        private void AddCurrentView()
        {
            CacheRect();
            views.Add(new ViewTransform
            {
                position = rect.anchoredPosition3D, rotation = rect.localEulerAngles,
                scale = rect.localScale, duration = 1f
            });
        }

        [Button("Save Start Position")]
        private void SaveStartPosition()
        {
            CacheRect();
            startPosition = rect.anchoredPosition3D;
            startRotation = rect.localEulerAngles;
            startScale = rect.localScale;
        }

        [Button("Back to Start")]
        public void BackToStart()
        {
            CacheRect();
            activeAnimation.Cancel();
            activeAnimation = UIAnimationRunner.Play(new UIAnimation
            {
                Owner = this, Rect = rect,
                Properties = UIAnimationProperties.Position | UIAnimationProperties.Rotation | UIAnimationProperties.Scale,
                Position = startPosition, Rotation = Quaternion.Euler(startRotation), Scale = startScale,
                Duration = backToStartDuration, Ease = UIEase.InOutSine
            });
        }

        [Button("Try Play Sequence")]
        public void TryPlay()
        {
            CacheRect();
            activeAnimation.Cancel();
            frameCount = 0;
            totalDuration = 0f;
            if (views == null || views.Count == 0) return;
            if (frames.Length < views.Count) Array.Resize(ref frames, views.Count);
            Vector3 position = rect.anchoredPosition3D, scale = rect.localScale;
            Quaternion rotation = rect.localRotation;
            foreach (var view in views)
            {
                if (view == null) continue;
                float duration = float.IsNaN(view.duration) || float.IsInfinity(view.duration) ? 0f : Mathf.Max(0f, view.duration);
                frames[frameCount++] = new Frame
                {
                    FromPosition = position, Position = view.position, FromScale = scale, Scale = view.scale,
                    FromRotation = rotation, Rotation = Quaternion.Euler(view.rotation),
                    Start = totalDuration, Duration = duration
                };
                position = view.position;
                scale = view.scale;
                rotation = Quaternion.Euler(view.rotation);
                totalDuration += duration;
            }
            if (frameCount == 0) return;
            activeAnimation = UIAnimationRunner.Play(new UIAnimation
            {
                Owner = this, Duration = totalDuration, Ease = UIEase.Linear, Receiver = this
            });
        }

        void IUIAnimationReceiver.ApplyAnimation(float progress)
        {
            if (rect == null) return;
            float elapsed = progress * totalDuration;
            for (int i = 0; i < frameCount; i++)
            {
                Frame frame = frames[i];
                if (elapsed >= frame.Start + frame.Duration && i < frameCount - 1) continue;
                float t = frame.Duration <= 0f ? 1f : Mathf.Clamp01((elapsed - frame.Start) / frame.Duration);
                rect.anchoredPosition3D = Vector3.LerpUnclamped(frame.FromPosition, frame.Position, UIEasing.Evaluate(UIEase.InOutQuad, t));
                float transformT = UIEasing.Evaluate(UIEase.OutQuad, t);
                rect.localRotation = Quaternion.SlerpUnclamped(frame.FromRotation, frame.Rotation, transformT);
                rect.localScale = Vector3.LerpUnclamped(frame.FromScale, frame.Scale, transformT);
                return;
            }
        }

        void IUIAnimationReceiver.AnimationCompleted() => activeAnimation = default;
        private void CacheRect() { if (rect == null) rect = GetComponent<RectTransform>(); }
    }
}
