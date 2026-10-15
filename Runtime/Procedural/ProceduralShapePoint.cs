using System;
using UnityEngine;

[Serializable]
public struct ProceduralShapePoint
{
    #region Inspector

    [SerializeField] private Vector2 _position;
    [SerializeField, Min(0f)] private float _radius;

    #endregion

    #region Properties

    public Vector2 Position
    {
        get => _position;
        set => _position = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
    }

    public float Radius
    {
        get => _radius;
        set => _radius = Mathf.Max(0f, value);
    }

    #endregion

    #region Constructor

    public ProceduralShapePoint(Vector2 position, float radius = 0f)
    {
        _position = new Vector2(Mathf.Clamp01(position.x), Mathf.Clamp01(position.y));
        _radius = Mathf.Max(0f, radius);
    }

    #endregion
}
