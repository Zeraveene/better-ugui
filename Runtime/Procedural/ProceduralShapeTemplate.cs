using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "ProceduralShapeTemplate",
    menuName = "Summertime/UI/Procedural Shape Template",
    order = 2)]
[MovedFrom(true, sourceAssembly: "MaxsusWorks.ProceduralUI")]
public sealed class ProceduralShapeTemplate : ScriptableObject
{
    #region Inspector

    [SerializeField] private List<Vector2> _normalizedPoints = new List<Vector2>
    {
        new Vector2(0f, 0f),
        new Vector2(0.5f, 1f),
        new Vector2(1f, 0f)
    };

    #endregion

    #region Callback

    public event Action Changed;

    #endregion

    #region Properties

    public IReadOnlyList<Vector2> NormalizedPoints => _normalizedPoints;

    #endregion

    #region Public Methods

    public void SetPoints(IList<Vector2> points)
    {
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        if (points.Count < 3)
            throw new ArgumentException("A procedural shape template requires at least three points.", nameof(points));

        _normalizedPoints.Clear();
        for (int i = 0; i < points.Count; i++)
            _normalizedPoints.Add(Clamp01(points[i]));
        Changed?.Invoke();
    }

    #endregion

    #region Editor

#if UNITY_EDITOR
    private void OnValidate()
    {
        _normalizedPoints ??= new List<Vector2>();
        while (_normalizedPoints.Count < 3)
            _normalizedPoints.Add(DefaultPoint(_normalizedPoints.Count));
        for (int i = 0; i < _normalizedPoints.Count; i++)
            _normalizedPoints[i] = Clamp01(_normalizedPoints[i]);
        Changed?.Invoke();
    }
#endif

    #endregion

    #region Private Methods

    private static Vector2 Clamp01(Vector2 point)
    {
        return new Vector2(Mathf.Clamp01(point.x), Mathf.Clamp01(point.y));
    }

    private static Vector2 DefaultPoint(int index)
    {
        switch (index)
        {
            case 0: return new Vector2(0f, 0f);
            case 1: return new Vector2(0.5f, 1f);
            default: return new Vector2(1f, 0f);
        }
    }

    #endregion
}
