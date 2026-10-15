using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public enum UIGradientType
{
    Linear,
    Corner,
    MultiStop,
    SingleColor
}

public enum UIGradientBlendMode
{
    Override,
    Multiply
}

[Serializable]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class UIGradientSettings
{
    #region Inspector
    [SerializeField] private UIGradientType _gradientType;
    [SerializeField, Range(0f, 1f)] private float _intensity = 1f;
    [SerializeField, Range(0f, 360f)] private float _angle;
    [SerializeField] private Color _singleColor = Color.white;
    [SerializeField] private Color _startColor = Color.red;
    [SerializeField] private Color _endColor = Color.yellow;
    [SerializeField] private Color _upperLeftColor = Color.red;
    [SerializeField] private Color _upperRightColor = Color.yellow;
    [SerializeField] private Color _lowerRightColor = Color.green;
    [SerializeField] private Color _lowerLeftColor = Color.blue;
    [SerializeField] private Gradient _gradient = CreateDefaultGradient();
    #endregion

    #region Callback
    [NonSerialized] internal Action Changed;
    #endregion

    #region Properties
    public UIGradientType GradientType
    {
        get => _gradientType;
        set => SetValue(ref _gradientType, value);
    }

    public float Intensity
    {
        get => _intensity;
        set => SetValue(ref _intensity, Mathf.Clamp01(value));
    }

    public float Angle
    {
        get => _angle;
        set => SetValue(ref _angle, NormalizeAngle(value));
    }

    public Color SingleColor
    {
        get => _singleColor;
        set => SetValue(ref _singleColor, value);
    }

    public Color StartColor
    {
        get => _startColor;
        set => SetValue(ref _startColor, value);
    }

    public Color EndColor
    {
        get => _endColor;
        set => SetValue(ref _endColor, value);
    }

    public Color UpperLeftColor
    {
        get => _upperLeftColor;
        set => SetValue(ref _upperLeftColor, value);
    }

    public Color UpperRightColor
    {
        get => _upperRightColor;
        set => SetValue(ref _upperRightColor, value);
    }

    public Color LowerRightColor
    {
        get => _lowerRightColor;
        set => SetValue(ref _lowerRightColor, value);
    }

    public Color LowerLeftColor
    {
        get => _lowerLeftColor;
        set => SetValue(ref _lowerLeftColor, value);
    }

    public Gradient Gradient
    {
        get => _gradient;
        set
        {
            Gradient next = CloneGradient(value ?? CreateDefaultGradient());
            if (GradientsEqual(_gradient, next))
                return;

            _gradient = next;
            Changed?.Invoke();
        }
    }
    #endregion

    #region Public Methods
    public void CopyFrom(UIGradientSettings source)
    {
        if (source == null)
            return;

        _gradientType = source._gradientType;
        _intensity = source._intensity;
        _angle = source._angle;
        _singleColor = source._singleColor;
        _startColor = source._startColor;
        _endColor = source._endColor;
        _upperLeftColor = source._upperLeftColor;
        _upperRightColor = source._upperRightColor;
        _lowerRightColor = source._lowerRightColor;
        _lowerLeftColor = source._lowerLeftColor;
        _gradient = CloneGradient(source._gradient);
        Changed?.Invoke();
    }

    public UIGradientSettings Clone()
    {
        var clone = new UIGradientSettings();
        clone.CopyFrom(this);
        return clone;
    }

    public Color Evaluate(Vector2 normalizedPosition)
    {
        if (_gradientType == UIGradientType.SingleColor)
            return _singleColor;

        Vector2 rotatedPosition = RotateNormalizedPosition(normalizedPosition, _angle);

        switch (_gradientType)
        {
            case UIGradientType.Linear:
                return Color.Lerp(_startColor, _endColor, rotatedPosition.y);

            case UIGradientType.Corner:
                Color lower = Color.Lerp(_lowerLeftColor, _lowerRightColor, rotatedPosition.x);
                Color upper = Color.Lerp(_upperLeftColor, _upperRightColor, rotatedPosition.x);
                return Color.Lerp(lower, upper, rotatedPosition.y);

            case UIGradientType.MultiStop:
                return (_gradient ?? CreateDefaultGradient()).Evaluate(rotatedPosition.y);

            default:
                return Color.white;
        }
    }

    public Color Blend(
        Color sourceColor,
        Color gradientColor,
        UIGradientBlendMode blendMode)
    {
        Color target = blendMode == UIGradientBlendMode.Multiply
            ? sourceColor * gradientColor
            : gradientColor;

        return Color.Lerp(sourceColor, target, _intensity);
    }

    public void GetMultiStopTimes(List<float> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        if (_gradientType != UIGradientType.MultiStop || _gradient == null)
            return;

        GradientColorKey[] colorKeys = _gradient.colorKeys;
        for (int i = 0; i < colorKeys.Length; i++)
            AddUniqueInteriorTime(results, colorKeys[i].time);

        GradientAlphaKey[] alphaKeys = _gradient.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
            AddUniqueInteriorTime(results, alphaKeys[i].time);

        results.Sort();
    }

    public void ReverseDirection()
    {
        Angle = _angle + 180f;
    }
    #endregion

    #region Internal Methods
    internal void NotifyChanged()
    {
        _intensity = Mathf.Clamp01(_intensity);
        _angle = NormalizeAngle(_angle);
        _gradient ??= CreateDefaultGradient();
        Changed?.Invoke();
    }

    internal float GetLinearCoordinate(Vector2 normalizedPosition)
    {
        return RotateNormalizedPosition(normalizedPosition, _angle).y;
    }
    #endregion

    #region Private Methods
    private static Gradient CreateDefaultGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.5137255f, 0.227451f, 0.7058824f), 0f),
                new GradientColorKey(new Color(0.9921569f, 0.1137255f, 0.1137255f), 0.5f),
                new GradientColorKey(new Color(0.9882353f, 0.6901961f, 0.2705882f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });
        return gradient;
    }

    private static Gradient CloneGradient(Gradient source)
    {
        if (source == null)
            return CreateDefaultGradient();

        var clone = new Gradient
        {
            mode = source.mode,
            colorSpace = source.colorSpace
        };
        clone.SetKeys(source.colorKeys, source.alphaKeys);
        return clone;
    }

    private static bool GradientsEqual(Gradient left, Gradient right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left == null || right == null || left.mode != right.mode || left.colorSpace != right.colorSpace)
            return false;

        GradientColorKey[] leftColors = left.colorKeys;
        GradientColorKey[] rightColors = right.colorKeys;
        GradientAlphaKey[] leftAlphas = left.alphaKeys;
        GradientAlphaKey[] rightAlphas = right.alphaKeys;

        if (leftColors.Length != rightColors.Length || leftAlphas.Length != rightAlphas.Length)
            return false;

        for (int i = 0; i < leftColors.Length; i++)
        {
            if (leftColors[i].color != rightColors[i].color || !Mathf.Approximately(leftColors[i].time, rightColors[i].time))
                return false;
        }

        for (int i = 0; i < leftAlphas.Length; i++)
        {
            if (!Mathf.Approximately(leftAlphas[i].alpha, rightAlphas[i].alpha)
                || !Mathf.Approximately(leftAlphas[i].time, rightAlphas[i].time))
                return false;
        }

        return true;
    }

    private static Vector2 RotateNormalizedPosition(Vector2 position, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        float sine = Mathf.Sin(radians);
        float cosine = Mathf.Cos(radians);
        float scale = Mathf.Abs(sine) + Mathf.Abs(cosine);
        Vector2 centered = position - Vector2.one * 0.5f;
        Vector2 rotated = new Vector2(
            cosine * centered.x - sine * centered.y,
            sine * centered.x + cosine * centered.y);

        return rotated / Mathf.Max(scale, Mathf.Epsilon) + Vector2.one * 0.5f;
    }

    private static float NormalizeAngle(float angle)
    {
        float normalized = angle % 360f;
        return normalized < 0f ? normalized + 360f : normalized;
    }

    private static void AddUniqueInteriorTime(List<float> results, float time)
    {
        if (time <= 0.0001f || time >= 0.9999f)
            return;

        for (int i = 0; i < results.Count; i++)
        {
            if (Mathf.Approximately(results[i], time))
                return;
        }

        results.Add(time);
    }

    private void SetValue<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        Changed?.Invoke();
    }
    #endregion
}
