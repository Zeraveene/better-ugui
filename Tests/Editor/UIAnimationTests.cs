using System;
using System.Collections;
using Zeraveene.BetterUGUI.Animation;
using Zeraveene.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Zeraveene.BetterUGUI.Tests
{
    public sealed class AnimationProbe : MonoBehaviour, IUIAnimationReceiver
    {
        public int Completions;
        public int Shows, Hides;
        public void Shown() => ++Shows;
        public void Hidden() => ++Hides;
        public float Progress;
        public Action Completed;
        public void ApplyAnimation(float progress) => Progress = progress;
        public void AnimationCompleted() { ++Completions; Completed?.Invoke(); }
    }

    public sealed class UIAnimationTests
    {
        private GameObject go;
        private RectTransform rect;
        private CanvasGroup group;
        private AnimationProbe probe;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("Animation Test", typeof(RectTransform), typeof(CanvasGroup), typeof(AnimationProbe));
            rect = go.GetComponent<RectTransform>();
            group = go.GetComponent<CanvasGroup>();
            probe = go.GetComponent<AnimationProbe>();
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
            UIAnimationRunner.Advance(0f, 0f);
            Assert.That(UIAnimationRunner.ActiveCount, Is.Zero);
        }

        private Animation.UIAnimation Record(float duration = 1f) => new Animation.UIAnimation
        {
            Owner = probe, Rect = rect, Group = group,
            Properties = UIAnimationProperties.Position | UIAnimationProperties.Scale | UIAnimationProperties.Alpha,
            Position = new Vector3(100f, 40f, 3f), Scale = Vector3.one * 2f,
            Alpha = 0f, Duration = duration, Ease = UIEase.Linear, Receiver = probe
        };

        [Test]
        public void CombinedPropertiesAndCompletion()
        {
            var handle = UIAnimationRunner.PlayManual(Record());
            UIAnimationRunner.Advance(0.5f, 0.5f);
            Assert.That(rect.anchoredPosition3D, Is.EqualTo(new Vector3(50f, 20f, 1.5f)));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one * 1.5f));
            Assert.That(group.alpha, Is.EqualTo(0.5f));
            Assert.That(handle.IsActive, Is.True);
            UIAnimationRunner.Advance(0.5f, 0.5f);
            Assert.That(handle.IsActive, Is.False);
            Assert.That(probe.Completions, Is.EqualTo(1));
            Assert.That(rect.anchoredPosition3D, Is.EqualTo(new Vector3(100f, 40f, 3f)));
        }

        [Test]
        public void CancelledHandleCannotCancelReusedSlot()
        {
            var old = UIAnimationRunner.PlayManual(Record());
            old.Cancel();
            var current = UIAnimationRunner.PlayManual(Record());
            old.Cancel();
            old.Complete();
            Assert.That(current.IsActive, Is.True);
            Assert.That(probe.Completions, Is.Zero);
            current.Cancel();
        }

        [Test]
        public void SwapRemovalDoesNotSkipOrDoubleAdvance()
        {
            var first = UIAnimationRunner.PlayManual(Record(0.1f));
            var second = UIAnimationRunner.PlayManual(Record());
            UIAnimationRunner.Advance(0.1f, 0.1f);
            Assert.That(first.IsActive, Is.False);
            Assert.That(probe.Progress, Is.EqualTo(0.1f).Within(0.0001f));
            second.Cancel();
        }

        [Test]
        public void CompletionCanStartAnotherAnimationWithoutAdvancingItThisFrame()
        {
            UIAnimationHandle next = default;
            probe.Completed = () => next = UIAnimationRunner.PlayManual(Record());
            UIAnimationRunner.PlayManual(Record(0.1f));
            UIAnimationRunner.Advance(0.2f, 0.2f);
            Assert.That(next.IsActive, Is.True);
            Assert.That(probe.Completions, Is.EqualTo(1));
            Assert.That(probe.Progress, Is.EqualTo(1f));
            probe.Completed = null;
            next.Cancel();
        }

        [Test]
        public void ZeroDurationCompletesSynchronously()
        {
            var handle = UIAnimationRunner.PlayManual(Record(0f));
            Assert.That(handle.IsActive, Is.False);
            Assert.That(probe.Progress, Is.EqualTo(1f));
            Assert.That(probe.Completions, Is.EqualTo(1));
        }

        [Test]
        public void DisabledOwnerCancelsWithoutCompleting()
        {
            var handle = UIAnimationRunner.PlayManual(Record());
            probe.enabled = false;
            UIAnimationRunner.Advance(1f, 1f);
            Assert.That(handle.IsActive, Is.False);
            Assert.That(probe.Completions, Is.Zero);
        }

        [Test]
        public void DestroyedOwnerCancelsWithoutCompleting()
        {
            var handle = UIAnimationRunner.PlayManual(Record());
            UnityEngine.Object.DestroyImmediate(go);
            UIAnimationRunner.Advance(1f, 1f);
            Assert.That(handle.IsActive, Is.False);
        }

        [Test]
        public void ScaledAndUnscaledTimeAreIndependent()
        {
            var record = Record();
            var scaled = UIAnimationRunner.PlayManual(record);
            UIAnimationRunner.Advance(0f, 0.5f);
            Assert.That(probe.Progress, Is.Zero);
            scaled.Cancel();
            record.UnscaledTime = true;
            var unscaled = UIAnimationRunner.PlayManual(record);
            UIAnimationRunner.Advance(0f, 0.5f);
            Assert.That(probe.Progress, Is.EqualTo(0.5f));
            unscaled.Cancel();
        }

        [Test]
        public void CompleteInvalidatesHandleBeforeCallback()
        {
            var handle = UIAnimationRunner.PlayManual(Record());
            probe.Completed = () => Assert.That(handle.IsActive, Is.False);
            handle.Complete();
            handle.Complete();
            Assert.That(probe.Completions, Is.EqualTo(1));
        }

        [Test]
        public void CapacityGrowthRetainsHandles()
        {
            int size = UIAnimationRunner.Capacity + 1;
            var handles = new UIAnimationHandle[size];
            for (int i = 0; i < size; i++) handles[i] = UIAnimationRunner.PlayManual(Record());
            foreach (var handle in handles) Assert.That(handle.IsActive, Is.True);
            foreach (var handle in handles) handle.Cancel();
        }

        [Test]
        public void ReservedStartAndUpdateAllocateNoManagedMemory()
        {
            UIAnimationRunner.Reserve(256);
            Animation.UIAnimation record = Record(0.01f);
            record.Receiver = null;
            for (int i = 0; i < 20; i++)
            {
                UIAnimationRunner.PlayManual(record);
                UIAnimationRunner.Advance(0.02f, 0.02f);
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                UIAnimationRunner.PlayManual(record);
                UIAnimationRunner.Advance(0.02f, 0.02f);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero, "Starting and updating reserved animation records must not allocate");
        }

        [Test]
        public void EasingValuesAndEndpointsPreservePresets()
        {
            Assert.That((int)UIEase.InOutQuad, Is.EqualTo(7));
            Assert.That((int)UIEase.Custom, Is.EqualTo(37));
            foreach (UIEase ease in Enum.GetValues(typeof(UIEase)))
            {
                if (ease == UIEase.Zero) continue;
                Assert.That(UIEasing.Evaluate(ease, 0f), Is.EqualTo(0f).Within(0.0001f), ease.ToString());
                Assert.That(UIEasing.Evaluate(ease, 1f), Is.EqualTo(1f).Within(0.0001f), ease.ToString());
                Assert.That(float.IsNaN(UIEasing.Evaluate(ease, 0.4f)), Is.False);
            }
            Assert.That(UIEasing.Evaluate(UIEase.OutBack, 0.7f), Is.GreaterThan(1f));
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 2f);
            Assert.That(UIEasing.Evaluate(UIEase.Custom, 0.5f, curve), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void PanelWithoutTransitionsCompletesAndSetsInteractionImmediately()
        {
            var panel = go.AddComponent<UIBase>();
            int shown = 0, hidden = 0;
            panel.OnCompleteShow = () => ++shown;
            panel.OnCompleteHide = () => ++hidden;
            panel.Show();
            Assert.That(shown, Is.EqualTo(1));
            Assert.That(group.interactable && group.blocksRaycasts, Is.True);
            panel.Hide();
            Assert.That(hidden, Is.EqualTo(1));
            Assert.That(go.activeSelf, Is.False);
            Assert.That(group.interactable || group.blocksRaycasts, Is.False);
        }

        [Test]
        public void ShapeForwardReverseAndPreviewUseSameEvaluator()
        {
            var shape = go.AddComponent<ProceduralShapeCustom>();
            var animation = go.AddComponent<ProceduralShapeCustomAnimation>();
            animation.SetTracks(new[] { new ProceduralShapeCustomAnimationTrack(0, Vector2.zero, Vector2.one) });
            animation.Duration = 0f;
            animation.PlayForward();
            Assert.That(shape.Points[0].Position, Is.EqualTo(Vector2.one));
            animation.PlayReverse();
            Assert.That(shape.Points[0].Position, Is.EqualTo(Vector2.zero));
            animation.Ease = UIEase.Linear;
            animation.BeginPreview(true);
            animation.EvaluatePreview(0.5f);
            Assert.That(shape.Points[0].Position, Is.EqualTo(Vector2.one * 0.5f));
        }

        [Test]
        public void ViewSequenceCompletesAndBackToStartUsesAnchoredCoordinates()
        {
            var sequence = go.AddComponent<UIViewSequence>();
            sequence.views.Add(new UIViewSequence.ViewTransform
            { position = new Vector3(10f, 20f, 0f), rotation = new Vector3(0f, 0f, 90f), scale = Vector3.one * 2f, duration = 0f });
            sequence.TryPlay();
            Assert.That(rect.anchoredPosition3D, Is.EqualTo(new Vector3(10f, 20f, 0f)));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one * 2f));
            sequence.startScale = Vector3.one;
            sequence.BackToStart();
            Assert.That(rect.anchoredPosition3D, Is.EqualTo(Vector3.zero));
            Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator RuntimePanelInterruptionDoesNotFireStaleHideCompletion()
        {
            yield return new EnterPlayMode();
            // Exercise the actual MonoBehaviour Update runner in Play Mode.
            var runtimeGo = new GameObject("Runtime panel test", typeof(RectTransform), typeof(CanvasGroup), typeof(UIBase), typeof(AnimationProbe));
            var panel = runtimeGo.GetComponent<UIBase>();
            Assert.That(panel, Is.Not.Null, "Panel component should be attached");
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(UIBase).GetField("enableFade", fields).SetValue(panel, true);
            typeof(UIBase).GetField("enableScale", fields).SetValue(panel, true);
            typeof(UIBase).GetField("enableSlide", fields).SetValue(panel, true);
            typeof(UIBase).GetField("transitionDuration", fields).SetValue(panel, 0.03f);
            var runtimeProbe = runtimeGo.GetComponent<AnimationProbe>();
            panel.OnCompleteHide = runtimeProbe.Hidden;
            panel.OnCompleteShow = runtimeProbe.Shown;
            panel.Show();
            panel.Hide();
            panel.Show();
            float timeout = Time.realtimeSinceStartup + 2f;
            while (UIAnimationRunner.ActiveCount > 0 && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(runtimeProbe.Shows, Is.EqualTo(1));
            Assert.That(runtimeProbe.Hides, Is.Zero);
            Assert.That(runtimeGo.activeSelf, Is.True);
            panel.Hide();
            timeout = Time.realtimeSinceStartup + 2f;
            while (runtimeGo.activeSelf && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(runtimeGo.activeSelf, Is.False);
            Assert.That(runtimeProbe.Hides, Is.EqualTo(1));
            UnityEngine.Object.Destroy(runtimeGo);
            yield return null;
            yield return new ExitPlayMode();
        }
    }
}
