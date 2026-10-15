using UnityEngine.Scripting.APIUpdating;
using System;
using UnityEngine;

namespace Zeraveene.BetterUGUI.Animation
{
    [AddComponentMenu("")]
    [MovedFrom(true, sourceNamespace: "MaxsusWorks.ProceduralUI.Animation", sourceAssembly: "MaxsusWorks.ProceduralUI")]
    public sealed class UIAnimationRunner : MonoBehaviour
    {
        private struct Entry
        {
            internal UIAnimation Animation;
            internal Vector3 FromPosition, FromScale;
            internal Quaternion FromRotation;
            internal float FromAlpha, Elapsed;
            internal int Slot;
            internal ulong BornTick;
        }
        private struct Slot { internal int Index; internal ulong Version; }
        private static Entry[] entries = new Entry[64];
        private static Slot[] slots = new Slot[64];
        private static int[] freeSlots = new int[64];
        private static int count, slotCount, freeCount;
        private static ulong version, tick;
        private static bool updating;
        private static UIAnimationRunner instance;
        public static int ActiveCount => count;
        public static int Capacity => entries.Length;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Array.Clear(entries, 0, entries.Length);
            Array.Clear(slots, 0, slots.Length);
            count = slotCount = freeCount = 0;
            updating = false;
            instance = null;
            // Keep the generation counter: handles retained with domain reload disabled stay invalid.
        }

        public static void Reserve(int capacity)
        {
            if (capacity <= entries.Length) return;
            Array.Resize(ref entries, capacity);
            Array.Resize(ref slots, capacity);
            Array.Resize(ref freeSlots, capacity);
        }

        public static UIAnimationHandle Play(UIAnimation animation) => Start(animation, false);
        internal static UIAnimationHandle PlayManual(UIAnimation animation) => Start(animation, true);

        private static UIAnimationHandle Start(UIAnimation animation, bool manual)
        {
            if (animation.Owner == null || !animation.Owner.isActiveAndEnabled) return default;
            if (float.IsNaN(animation.Duration) || float.IsInfinity(animation.Duration))
                throw new ArgumentOutOfRangeException(nameof(animation.Duration));
            var entry = new Entry { Animation = animation, BornTick = tick };
            if (animation.Rect != null)
            {
                entry.FromPosition = animation.Rect.anchoredPosition3D;
                entry.FromScale = animation.Rect.localScale;
                entry.FromRotation = animation.Rect.localRotation;
            }
            if (animation.Group != null) entry.FromAlpha = animation.Group.alpha;
            if (animation.Duration <= 0f || (!Application.isPlaying && !manual))
            {
                Apply(entry, 1f);
                if (animation.Owner != null && animation.Owner.isActiveAndEnabled)
                    animation.Receiver?.AnimationCompleted();
                return default;
            }
            if (count == entries.Length) Reserve(entries.Length * 2);
            int slot = freeCount > 0 ? freeSlots[--freeCount] : slotCount++;
            var handle = new UIAnimationHandle(slot, ++version);
            slots[slot] = new Slot { Index = count, Version = handle.Version };
            entry.Slot = slot;
            entries[count++] = entry;
            if (!manual) EnsureRunner();
            return handle;
        }

        public static bool IsActive(UIAnimationHandle handle) => handle.Version != 0
            && handle.Slot >= 0 && handle.Slot < slotCount && slots[handle.Slot].Version == handle.Version;

        public static void Cancel(UIAnimationHandle handle)
        {
            if (IsActive(handle)) Remove(slots[handle.Slot].Index);
        }

        public static void Complete(UIAnimationHandle handle)
        {
            if (!IsActive(handle)) return;
            int index = slots[handle.Slot].Index;
            Entry entry = entries[index];
            Remove(index); // Invalidate before user code can start/cancel more animations.
            if (!IsAlive(entry.Animation)) return;
            Apply(entry, 1f);
            if (IsAlive(entry.Animation)) entry.Animation.Receiver?.AnimationCompleted();
        }

        private static bool IsAlive(UIAnimation a) => a.Owner != null && a.Owner.isActiveAndEnabled
            && ((a.Properties & (UIAnimationProperties.Position | UIAnimationProperties.Scale | UIAnimationProperties.Rotation)) == 0 || a.Rect != null)
            && ((a.Properties & UIAnimationProperties.Alpha) == 0 || a.Group != null);

        private static void EnsureRunner()
        {
            if (instance == null)
            {
                var go = new GameObject("Better uGUI Animation Runner") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<UIAnimationRunner>();
            }
            instance.enabled = true;
        }

        private void Update() => Advance(Time.deltaTime, Time.unscaledDeltaTime);

        // Internal deterministic entry point is also used by the package's editor tests.
        internal static void Advance(float scaledDelta, float unscaledDelta)
        {
            if (updating) return;
            updating = true;
            ++tick;
            try
            {
                int i = 0;
                while (i < count)
                {
                    Entry entry = entries[i];
                    if (entry.BornTick == tick) { ++i; continue; }
                    if (!IsAlive(entry.Animation)) { Remove(i); continue; }
                    var handle = new UIAnimationHandle(entry.Slot, slots[entry.Slot].Version);
                    entry.Elapsed += Mathf.Max(0f, entry.Animation.UnscaledTime ? unscaledDelta : scaledDelta);
                    entries[i].Elapsed = entry.Elapsed;
                    float t = Mathf.Clamp01(entry.Elapsed / entry.Animation.Duration);
                    // Remove completed records before invoking callbacks. Swap removal keeps storage dense.
                    if (t >= 1f) Remove(i);
                    try
                    {
                        Apply(entry, UIEasing.Evaluate(entry.Animation.Ease, t, entry.Animation.Curve));
                        if (t >= 1f && IsAlive(entry.Animation)) entry.Animation.Receiver?.AnimationCompleted();
                    }
                    catch (Exception exception)
                    {
                        Cancel(handle);
                        Debug.LogException(exception, entry.Animation.Owner);
                    }
                    if (t < 1f && IsActive(handle) && slots[handle.Slot].Index == i) ++i;
                }
            }
            finally
            {
                updating = false;
                if (instance != null) instance.enabled = count > 0;
            }
        }

        private static void Apply(Entry entry, float t)
        {
            UIAnimation a = entry.Animation;
            if (a.Rect != null)
            {
                if ((a.Properties & UIAnimationProperties.Position) != 0)
                    a.Rect.anchoredPosition3D = Vector3.LerpUnclamped(entry.FromPosition, a.Position, t);
                if ((a.Properties & UIAnimationProperties.Scale) != 0)
                    a.Rect.localScale = Vector3.LerpUnclamped(entry.FromScale, a.Scale, t);
                if ((a.Properties & UIAnimationProperties.Rotation) != 0)
                    a.Rect.localRotation = Quaternion.SlerpUnclamped(entry.FromRotation, a.Rotation, t);
            }
            if ((a.Properties & UIAnimationProperties.Alpha) != 0 && a.Group != null)
                a.Group.alpha = Mathf.LerpUnclamped(entry.FromAlpha, a.Alpha, t);
            a.Receiver?.ApplyAnimation(t);
        }

        private static void Remove(int index)
        {
            int slot = entries[index].Slot;
            slots[slot] = default;
            freeSlots[freeCount++] = slot;
            --count;
            if (index != count)
            {
                entries[index] = entries[count];
                slots[entries[index].Slot].Index = index;
            }
            entries[count] = default;
            if (count == 0 && instance != null) instance.enabled = false;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            ResetState();
        }
    }
}
