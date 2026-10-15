using System;
using System.Collections.Generic;
using Zeraveene.BetterUGUI.Animation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class ProceduralShapeCustomAnimationTrack
{
    #region Inspector

    [SerializeField, Min(0)] private int _pointIndex;
    [SerializeField] private Vector2 _from;
    [SerializeField] private Vector2 _to;

    #endregion

    #region Properties

    public int PointIndex
    {
        get => _pointIndex;
        set => _pointIndex = Mathf.Max(0, value);
    }

    public Vector2 From
    {
        get => _from;
        set => _from = Clamp01(value);
    }

    public Vector2 To
    {
        get => _to;
        set => _to = Clamp01(value);
    }

    #endregion

    #region Constructor

    public ProceduralShapeCustomAnimationTrack()
    {
    }

    public ProceduralShapeCustomAnimationTrack(int pointIndex, Vector2 from, Vector2 to)
    {
        PointIndex = pointIndex;
        From = from;
        To = to;
    }

    #endregion

    #region Methods

    internal void ClampValues()
    {
        _pointIndex = Mathf.Max(0, _pointIndex);
        _from = Clamp01(_from);
        _to = Clamp01(_to);
    }

    private static Vector2 Clamp01(Vector2 value)
    {
        return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    }

    #endregion
}

[DisallowMultipleComponent]
[RequireComponent(typeof(ProceduralShapeCustom))]
[AddComponentMenu("UI/Procedural Shape Custom Animation")]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class ProceduralShapeCustomAnimation : MonoBehaviour, IUIAnimationReceiver
{
    #region Inspector

    [SerializeField]
    private List<ProceduralShapeCustomAnimationTrack> _tracks = new List<ProceduralShapeCustomAnimationTrack>();

    [SerializeField, Min(0f)] private float _duration = 0.5f;
    [SerializeField] private UIEase _ease = UIEase.InOutSine;
    [SerializeField] private bool _playOnEnable = true;

    #endregion

    #region Runtime Data

    private ProceduralShapeCustom _shape;
    private UIAnimationHandle _activeTween;
    private struct ActiveTrack { internal int Index; internal Vector2 From, To; }
    private ActiveTrack[] _activeTracks = Array.Empty<ActiveTrack>();
    private int _activeTrackCount;

    #endregion

    #region Properties

    public IReadOnlyList<ProceduralShapeCustomAnimationTrack> Tracks => _tracks;

    public float Duration
    {
        get => _duration;
        set => _duration = Mathf.Max(0f, value);
    }

    public UIEase Ease
    {
        get => _ease;
        set => _ease = value;
    }

    public bool PlayOnEnable
    {
        get => _playOnEnable;
        set => _playOnEnable = value;
    }

    public bool IsPlaying => _activeTween.IsActive;

    #endregion

    #region Public Methods

    public void SetTracks(IList<ProceduralShapeCustomAnimationTrack> tracks)
    {
        if (tracks == null)
            throw new ArgumentNullException(nameof(tracks));

        _tracks.Clear();
        for (int i = 0; i < tracks.Count; i++)
        {
            ProceduralShapeCustomAnimationTrack track = tracks[i];
            if (track == null)
                continue;
            _tracks.Add(new ProceduralShapeCustomAnimationTrack(track.PointIndex, track.From, track.To));
        }
    }

    public UIAnimationHandle PlayForward()
    {
        return Play(true);
    }

    public UIAnimationHandle PlayReverse()
    {
        return Play(false);
    }

    public void StopAnimation()
    {
        _activeTween.Cancel();
        _activeTween = default;
    }

    #endregion

    #region Unity Events

    private void Awake()
    {
        CacheShape();
    }

    private void OnEnable()
    {
        if (Application.isPlaying && _playOnEnable)
            PlayForward();
    }

    private void OnDisable()
    {
        StopAnimation();
    }

    private void Reset()
    {
        CacheShape();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _duration = Mathf.Max(0f, _duration);
        if (_tracks == null)
            _tracks = new List<ProceduralShapeCustomAnimationTrack>();
        for (int i = 0; i < _tracks.Count; i++)
            _tracks[i]?.ClampValues();
        CacheShape();
    }
#endif

    #endregion

    #region Private Methods

    private UIAnimationHandle Play(bool forward)
    {
        PrepareTracks(forward);
        if (_activeTrackCount == 0) return default;
        _activeTween = UIAnimationRunner.Play(new UIAnimation
        {
            Owner = this, Duration = _duration, Ease = _ease, Receiver = this
        });
        return _activeTween;
    }

    private void PrepareTracks(bool forward)
    {
        CacheShape();
        StopAnimation();
        _activeTrackCount = 0;
        if (_shape == null || _tracks == null) return;
        if (_activeTracks.Length < _tracks.Count)
            Array.Resize(ref _activeTracks, _tracks.Count);
        for (int i = 0; i < _tracks.Count; i++)
        {
            var track = _tracks[i];
            if (track == null || track.PointIndex < 0 || track.PointIndex >= _shape.Points.Count) continue;
            bool duplicate = false;
            for (int j = 0; j < _activeTrackCount; j++)
                if (_activeTracks[j].Index == track.PointIndex) { duplicate = true; break; }
            if (duplicate) continue;
            _activeTracks[_activeTrackCount++] = new ActiveTrack
            {
                Index = track.PointIndex,
                From = forward ? track.From : track.To,
                To = forward ? track.To : track.From
            };
        }
        ApplyProgress(0f);
    }

    private void ApplyProgress(float progress)
    {
        if (_shape == null) return;
        for (int i = 0; i < _activeTrackCount; i++)
        {
            ActiveTrack track = _activeTracks[i];
            if (track.Index < _shape.Points.Count)
                _shape.SetPoint(track.Index, Vector2.LerpUnclamped(track.From, track.To, progress));
        }
    }

    void IUIAnimationReceiver.ApplyAnimation(float progress) => ApplyProgress(progress);
    void IUIAnimationReceiver.AnimationCompleted() => _activeTween = default;

#if UNITY_EDITOR
    // Preview is driven locally by the inspector; it never creates a runtime runner.
    internal void BeginPreview(bool forward) => PrepareTracks(forward);
    internal void EvaluatePreview(float progress) => ApplyProgress(UIEasing.Evaluate(_ease, progress));
#endif

    private void CacheShape()
    {
        if (_shape == null)
            _shape = GetComponent<ProceduralShapeCustom>();
    }

    #endregion
}
