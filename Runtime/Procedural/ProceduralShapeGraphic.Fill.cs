using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract partial class ProceduralShapeGraphic
{
    #region Runtime Data

    private readonly VertexHelper _unclippedMesh = new VertexHelper();
    private readonly List<UIVertex> _sourceTriangleStream = new List<UIVertex>(384);
    private readonly List<UIVertex> _clippedTriangleStream = new List<UIVertex>(384);
    private readonly List<UIVertex> _clipInput = new List<UIVertex>(8);
    private readonly List<UIVertex> _clipOutput = new List<UIVertex>(8);
    private readonly List<Vector2> _fillMaskTriangles = new List<Vector2>(24);
    private readonly Vector3[] _fillQuad = new Vector3[4];

    #endregion

    #region Fill Clipping

    private void ApplyFill(VertexHelper source, VertexHelper destination, Rect rect)
    {
        _sourceTriangleStream.Clear();
        _clippedTriangleStream.Clear();
        source.GetUIVertexStream(_sourceTriangleStream);
        BuildFillMask(rect, _sourceTriangleStream);
        for (int triangle = 0; triangle + 2 < _sourceTriangleStream.Count; triangle += 3)
        {
            for (int mask = 0; mask + 2 < _fillMaskTriangles.Count; mask += 3)
            {
                ClipTriangle(
                    _sourceTriangleStream[triangle],
                    _sourceTriangleStream[triangle + 1],
                    _sourceTriangleStream[triangle + 2],
                    _fillMaskTriangles[mask],
                    _fillMaskTriangles[mask + 1],
                    _fillMaskTriangles[mask + 2]);
            }
        }
        destination.AddUIVertexTriangleStream(_clippedTriangleStream);
    }

    private void BuildFillMask(Rect rect, List<UIVertex> source)
    {
        _fillMaskTriangles.Clear();
        float minX = rect.xMin, maxX = rect.xMax, minY = rect.yMin, maxY = rect.yMax;
        for (int i = 0; i < source.Count; i++)
        {
            Vector3 position = source[i].position;
            minX = Mathf.Min(minX, position.x); maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y); maxY = Mathf.Max(maxY, position.y);
        }
        float margin = Mathf.Max(maxX - minX, maxY - minY) + 1f;
        minX -= margin; maxX += margin; minY -= margin; maxY += margin;

        if (_fillMethod == Image.FillMethod.Horizontal)
        {
            float edge = _fillOriginHorizontal == Image.OriginHorizontal.Left
                ? Mathf.Lerp(rect.xMin, rect.xMax, _fillAmount)
                : Mathf.Lerp(rect.xMax, rect.xMin, _fillAmount);
            if (_fillOriginHorizontal == Image.OriginHorizontal.Left) maxX = edge; else minX = edge;
            AddMaskQuad(new Vector2(minX, minY), new Vector2(minX, maxY), new Vector2(maxX, maxY), new Vector2(maxX, minY));
            return;
        }
        if (_fillMethod == Image.FillMethod.Vertical)
        {
            float edge = _fillOriginVertical == Image.OriginVertical.Bottom
                ? Mathf.Lerp(rect.yMin, rect.yMax, _fillAmount)
                : Mathf.Lerp(rect.yMax, rect.yMin, _fillAmount);
            if (_fillOriginVertical == Image.OriginVertical.Bottom) maxY = edge; else minY = edge;
            AddMaskQuad(new Vector2(minX, minY), new Vector2(minX, maxY), new Vector2(maxX, maxY), new Vector2(maxX, minY));
            return;
        }

        float farRadius = Mathf.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
        BuildRadialMask(rect, farRadius);
    }

    private void BuildRadialMask(Rect rect, float farRadius)
    {
        if (_fillMethod == Image.FillMethod.Radial90)
        {
            SetQuad(rect, 0f, 0f, 1f, 1f);
            if (RadialCut(_fillQuad, _fillAmount, _fillClockwise, (int)_fillOrigin90))
                AddExtendedMaskQuad(_fillQuad, GetOrigin90Pivot(rect), farRadius);
            return;
        }

        if (_fillMethod == Image.FillMethod.Radial180)
        {
            int origin = (int)_fillOrigin180;
            int even = origin > 1 ? 1 : 0;
            Vector2 pivot = GetOrigin180Pivot(rect);
            for (int side = 0; side < 2; side++)
            {
                float fx0 = 0f, fx1 = 1f, fy0 = 0f, fy1 = 1f;
                if (origin == 0 || origin == 2)
                {
                    if (side == even) fx1 = 0.5f; else fx0 = 0.5f;
                }
                else
                {
                    if (side == even) fy0 = 0.5f; else fy1 = 0.5f;
                }
                SetQuad(rect, fx0, fy0, fx1, fy1);
                float value = _fillClockwise ? _fillAmount * 2f - side : _fillAmount * 2f - (1 - side);
                if (RadialCut(_fillQuad, Mathf.Clamp01(value), _fillClockwise, (side + origin + 3) % 4))
                    AddExtendedMaskQuad(_fillQuad, pivot, farRadius);
            }
            return;
        }

        Vector2 center = rect.center;
        int origin360 = (int)_fillOrigin360;
        for (int corner = 0; corner < 4; corner++)
        {
            float fx0 = corner < 2 ? 0f : 0.5f;
            float fx1 = corner < 2 ? 0.5f : 1f;
            float fy0 = corner == 0 || corner == 3 ? 0f : 0.5f;
            float fy1 = corner == 0 || corner == 3 ? 0.5f : 1f;
            SetQuad(rect, fx0, fy0, fx1, fy1);
            float value = _fillClockwise
                ? _fillAmount * 4f - ((corner + origin360) % 4)
                : _fillAmount * 4f - (3 - ((corner + origin360) % 4));
            if (RadialCut(_fillQuad, Mathf.Clamp01(value), _fillClockwise, (corner + 2) % 4))
                AddExtendedMaskQuad(_fillQuad, center, farRadius);
        }
    }

    private void SetQuad(Rect rect, float x0, float y0, float x1, float y1)
    {
        _fillQuad[0] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, x0), Mathf.Lerp(rect.yMin, rect.yMax, y0));
        _fillQuad[1] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, x0), Mathf.Lerp(rect.yMin, rect.yMax, y1));
        _fillQuad[2] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, x1), Mathf.Lerp(rect.yMin, rect.yMax, y1));
        _fillQuad[3] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, x1), Mathf.Lerp(rect.yMin, rect.yMax, y0));
    }

    private void AddExtendedMaskQuad(Vector3[] quad, Vector2 pivot, float radius)
    {
        Vector2 a = ExtendFromPivot(quad[0], pivot, radius);
        Vector2 b = ExtendFromPivot(quad[1], pivot, radius);
        Vector2 c = ExtendFromPivot(quad[2], pivot, radius);
        Vector2 d = ExtendFromPivot(quad[3], pivot, radius);
        AddMaskQuad(a, b, c, d);
    }

    private static Vector2 ExtendFromPivot(Vector2 point, Vector2 pivot, float radius)
    {
        Vector2 offset = point - pivot;
        return offset.sqrMagnitude < 0.0000001f ? pivot : pivot + offset.normalized * radius;
    }

    private Vector2 GetOrigin90Pivot(Rect rect)
    {
        switch (_fillOrigin90)
        {
            case Image.Origin90.TopLeft: return new Vector2(rect.xMin, rect.yMax);
            case Image.Origin90.TopRight: return new Vector2(rect.xMax, rect.yMax);
            case Image.Origin90.BottomRight: return new Vector2(rect.xMax, rect.yMin);
            default: return new Vector2(rect.xMin, rect.yMin);
        }
    }

    private Vector2 GetOrigin180Pivot(Rect rect)
    {
        switch (_fillOrigin180)
        {
            case Image.Origin180.Left: return new Vector2(rect.xMin, rect.center.y);
            case Image.Origin180.Top: return new Vector2(rect.center.x, rect.yMax);
            case Image.Origin180.Right: return new Vector2(rect.xMax, rect.center.y);
            default: return new Vector2(rect.center.x, rect.yMin);
        }
    }

    private void AddMaskQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        AddMaskTriangle(a, b, c);
        AddMaskTriangle(a, c, d);
    }

    private void AddMaskTriangle(Vector2 a, Vector2 b, Vector2 c)
    {
        if (Mathf.Abs(Cross(b - a, c - a)) < 0.000001f) return;
        _fillMaskTriangles.Add(a); _fillMaskTriangles.Add(b); _fillMaskTriangles.Add(c);
    }

    private void ClipTriangle(UIVertex a, UIVertex b, UIVertex c, Vector2 maskA, Vector2 maskB, Vector2 maskC)
    {
        _clipInput.Clear();
        _clipInput.Add(a); _clipInput.Add(b); _clipInput.Add(c);
        bool ccw = Cross(maskB - maskA, maskC - maskA) > 0f;
        ClipAgainstEdge(maskA, maskB, ccw);
        ClipAgainstEdge(maskB, maskC, ccw);
        ClipAgainstEdge(maskC, maskA, ccw);
        if (_clipInput.Count < 3) return;
        for (int i = 1; i < _clipInput.Count - 1; i++)
        {
            _clippedTriangleStream.Add(_clipInput[0]);
            _clippedTriangleStream.Add(_clipInput[i]);
            _clippedTriangleStream.Add(_clipInput[i + 1]);
        }
    }

    private void ClipAgainstEdge(Vector2 edgeA, Vector2 edgeB, bool ccw)
    {
        _clipOutput.Clear();
        if (_clipInput.Count == 0) return;
        UIVertex previous = _clipInput[_clipInput.Count - 1];
        float previousDistance = SignedEdgeDistance(previous.position, edgeA, edgeB, ccw);
        for (int i = 0; i < _clipInput.Count; i++)
        {
            UIVertex current = _clipInput[i];
            float currentDistance = SignedEdgeDistance(current.position, edgeA, edgeB, ccw);
            bool previousInside = previousDistance >= -0.00001f;
            bool currentInside = currentDistance >= -0.00001f;
            if (previousInside != currentInside)
            {
                float t = previousDistance / (previousDistance - currentDistance);
                _clipOutput.Add(LerpVertex(previous, current, t));
            }
            if (currentInside) _clipOutput.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }
        _clipInput.Clear();
        _clipInput.AddRange(_clipOutput);
    }

    private static float SignedEdgeDistance(Vector3 point, Vector2 a, Vector2 b, bool ccw)
    {
        float distance = Cross(b - a, (Vector2)point - a);
        return ccw ? distance : -distance;
    }

    private static UIVertex LerpVertex(UIVertex a, UIVertex b, float t)
    {
        UIVertex result = a;
        result.position = Vector3.LerpUnclamped(a.position, b.position, t);
        result.normal = Vector3.LerpUnclamped(a.normal, b.normal, t);
        result.tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t);
        result.color = (Color32)Color.LerpUnclamped(a.color, b.color, t);
        result.uv0 = Vector4.LerpUnclamped(a.uv0, b.uv0, t);
        result.uv1 = Vector4.LerpUnclamped(a.uv1, b.uv1, t);
        result.uv2 = Vector4.LerpUnclamped(a.uv2, b.uv2, t);
        result.uv3 = Vector4.LerpUnclamped(a.uv3, b.uv3, t);
        return result;
    }

    // Kept equivalent to Unity UI Image's radial fill calculation.
    private static bool RadialCut(Vector3[] xy, float fill, bool invert, int corner)
    {
        if (fill < 0.001f) return false;
        if ((corner & 1) == 1) invert = !invert;
        if (!invert && fill > 0.999f) return true;
        float angle = Mathf.Clamp01(fill);
        if (invert) angle = 1f - angle;
        angle *= 90f * Mathf.Deg2Rad;
        RadialCut(xy, Mathf.Cos(angle), Mathf.Sin(angle), invert, corner);
        return true;
    }

    private static void RadialCut(Vector3[] xy, float cos, float sin, bool invert, int corner)
    {
        int i0 = corner;
        int i1 = (corner + 1) % 4;
        int i2 = (corner + 2) % 4;
        int i3 = (corner + 3) % 4;
        if ((corner & 1) == 1)
        {
            if (sin > cos)
            {
                cos /= sin; sin = 1f;
                if (invert) { xy[i1].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos); xy[i2].x = xy[i1].x; }
            }
            else if (cos > sin)
            {
                sin /= cos; cos = 1f;
                if (!invert) { xy[i2].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin); xy[i3].y = xy[i2].y; }
            }
            else { cos = 1f; sin = 1f; }
            if (!invert) xy[i3].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
            else xy[i1].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
        }
        else
        {
            if (cos > sin)
            {
                sin /= cos; cos = 1f;
                if (!invert) { xy[i1].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin); xy[i2].y = xy[i1].y; }
            }
            else if (sin > cos)
            {
                cos /= sin; sin = 1f;
                if (invert) { xy[i2].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos); xy[i3].x = xy[i2].x; }
            }
            else { cos = 1f; sin = 1f; }
            if (invert) xy[i3].y = Mathf.Lerp(xy[i0].y, xy[i2].y, sin);
            else xy[i1].x = Mathf.Lerp(xy[i0].x, xy[i2].x, cos);
        }
    }

    #endregion
}
