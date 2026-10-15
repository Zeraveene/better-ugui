using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public abstract partial class ProceduralShapeGraphic
{
    #region Runtime Data

    private readonly List<int> _indices = new List<int>(64);
    private readonly List<int> _triangles = new List<int>(192);

    #endregion

    #region Polygon Geometry

    private bool AddFill(VertexHelper vh, List<Vector2> points, Rect rect)
    {
        int ignored;
        return AddFill(vh, points, rect, out ignored);
    }

    private bool AddFill(VertexHelper vh, List<Vector2> points, Rect rect, out int start)
    {
        start = -1;
        if (!Triangulate(points)) return false;
        start = vh.currentVertCount;
        for (int i = 0; i < points.Count; i++) AddVertex(vh, points[i], color, rect);
        for (int i = 0; i < _triangles.Count; i += 3)
            vh.AddTriangle(start + _triangles[i], start + _triangles[i + 1], start + _triangles[i + 2]);
        return true;
    }

    private static void AddRing(
        VertexHelper vh,
        List<Vector2> outer,
        List<Vector2> inner,
        Color32 outerColor,
        Color32 innerColor,
        Rect rect)
    {
        int count = Mathf.Min(outer.Count, inner.Count);
        int outerStart = AddLoop(vh, outer, outerColor, rect, count);
        int innerStart = AddLoop(vh, inner, innerColor, rect, count);
        AddRingTriangles(vh, outerStart, innerStart, count);
    }

    private static int AddLoop(VertexHelper vh, List<Vector2> points, Color32 vertexColor, Rect rect, int count = -1)
    {
        if (count < 0) count = points.Count;
        int start = vh.currentVertCount;
        for (int i = 0; i < count; i++) AddVertex(vh, points[i], vertexColor, rect);
        return start;
    }

    private static void AddRingTriangles(VertexHelper vh, int outerStart, int innerStart, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            vh.AddTriangle(outerStart + i, outerStart + next, innerStart + next);
            vh.AddTriangle(outerStart + i, innerStart + next, innerStart + i);
        }
    }

    private bool Triangulate(List<Vector2> points)
    {
        _triangles.Clear();
        _indices.Clear();
        float area = Area(points);
        if (points.Count < 3 || Mathf.Abs(area) < 0.000001f) return false;
        bool ccw = area > 0f;
        if (ProceduralShapeGeometryUtility.TryTriangulateConvex(points, ccw, _triangles)) return true;
        _triangles.Clear();
        for (int i = 0; i < points.Count; i++) _indices.Add(i);
        int guard = points.Count * points.Count;
        while (_indices.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < _indices.Count; i++)
            {
                int a = _indices[(i + _indices.Count - 1) % _indices.Count];
                int b = _indices[i];
                int c = _indices[(i + 1) % _indices.Count];
                float cross = Cross(points[b] - points[a], points[c] - points[b]);
                if (ccw ? cross <= 0.000001f : cross >= -0.000001f) continue;
                bool blocked = false;
                for (int j = 0; j < _indices.Count; j++)
                {
                    int p = _indices[j];
                    if (p != a && p != b && p != c && InTriangle(points[p], points[a], points[b], points[c])) { blocked = true; break; }
                }
                if (blocked) continue;
                AddTriangle(a, b, c, ccw);
                _indices.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) return false;
        }
        if (_indices.Count != 3) return false;
        AddTriangle(_indices[0], _indices[1], _indices[2], ccw);
        return true;
    }

    private void AddTriangle(int a, int b, int c, bool ccw)
    {
        _triangles.Add(a);
        _triangles.Add(ccw ? b : c);
        _triangles.Add(ccw ? c : b);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float x = Cross(b - a, p - a), y = Cross(c - b, p - b), z = Cross(a - c, p - c);
        return !(x < -0.000001f || y < -0.000001f || z < -0.000001f) || !(x > 0.000001f || y > 0.000001f || z > 0.000001f);
    }

    private static bool Contains(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i], edge = b - a;
            float t = edge.sqrMagnitude < 0.0000001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
            if ((point - (a + edge * t)).sqrMagnitude < 0.000001f) return true;
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private static void Clean(List<Vector2> points) => ProceduralShapeGeometryUtility.Clean(points);

    private static bool IsSimple(List<Vector2> points)
    {
        if (points.Count < 3) return false;
        float area = Area(points);
        if (Mathf.Abs(area) < 0.000001f) return false;
        if (ProceduralShapeGeometryUtility.IsConvex(points, area > 0f)) return true;
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                if (j == i || j == (i + 1) % points.Count || (j + 1) % points.Count == i) continue;
                if (Intersects(points[i], points[(i + 1) % points.Count], points[j], points[(j + 1) % points.Count])) return false;
            }
        return true;
    }

    private static bool IsUsableInset(List<Vector2> outer, List<Vector2> inset)
    {
        if (!IsSimple(inset)) return false;
        float outerArea = Area(outer);
        float insetArea = Area(inset);
        return outerArea * insetArea > 0f && Mathf.Abs(insetArea) < Mathf.Abs(outerArea);
    }

    private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return Cross(b - a, c - a) * Cross(b - a, d - a) < -0.0000001f &&
               Cross(d - c, a - c) * Cross(d - c, b - c) < -0.0000001f;
    }

    private static void Offset(List<Vector2> source, float inwardDistance, List<Vector2> output)
    {
        output.Clear();
        bool ccw = Area(source) > 0f;
        for (int i = 0; i < source.Count; i++)
        {
            Vector2 previous = source[(i + source.Count - 1) % source.Count];
            Vector2 current = source[i];
            Vector2 next = source[(i + 1) % source.Count];
            Vector2 n1 = Normal(current - previous, ccw), n2 = Normal(next - current, ccw);
            Vector2 miter = n1 + n2;
            if (miter.sqrMagnitude < 0.0000001f) miter = n1; else miter.Normalize();
            output.Add(current + miter * (inwardDistance / Mathf.Max(Mathf.Abs(Vector2.Dot(miter, n1)), 0.25f)));
        }
    }

    private static Vector2 Normal(Vector2 edge, bool ccw)
    {
        if (edge.sqrMagnitude < 0.0000001f) return Vector2.zero;
        edge.Normalize();
        return ccw ? new Vector2(-edge.y, edge.x) : new Vector2(edge.y, -edge.x);
    }

    private static float Area(IList<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++) area += Cross(points[i], points[(i + 1) % points.Count]);
        return area * 0.5f;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private void ClipToAxisBand(List<Vector2> source, bool horizontal, float min, float max, List<Vector2> output)
    {
        _dashClipInput.Clear();
        _dashClipInput.AddRange(source);
        ClipPointsAgainstAxis(horizontal, min, true);
        ClipPointsAgainstAxis(horizontal, max, false);
        output.Clear();
        output.AddRange(_dashClipInput);
    }

    private void ClipPointsAgainstAxis(bool horizontal, float edge, bool keepGreater)
    {
        _dashClipOutput.Clear();
        if (_dashClipInput.Count == 0) return;

        Vector2 previous = _dashClipInput[_dashClipInput.Count - 1];
        float previousValue = horizontal ? previous.x : previous.y;
        bool previousInside = keepGreater ? previousValue >= edge : previousValue <= edge;
        for (int i = 0; i < _dashClipInput.Count; i++)
        {
            Vector2 current = _dashClipInput[i];
            float currentValue = horizontal ? current.x : current.y;
            bool currentInside = keepGreater ? currentValue >= edge : currentValue <= edge;
            if (previousInside != currentInside)
            {
                float t = (edge - previousValue) / (currentValue - previousValue);
                _dashClipOutput.Add(Vector2.LerpUnclamped(previous, current, t));
            }
            if (currentInside) _dashClipOutput.Add(current);
            previous = current;
            previousValue = currentValue;
            previousInside = currentInside;
        }

        _dashClipInput.Clear();
        _dashClipInput.AddRange(_dashClipOutput);
    }

    private void RoundDashCuts(List<Vector2> contour, bool horizontal, float min, float max, float axisLength)
    {
        float roundness = Mathf.Clamp01(GetDashRoundness());
        if (roundness <= 0.000001f || contour.Count < 3) return;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        for (int i = 0; i < contour.Count; i++)
        {
            Vector2 point = contour[i];
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y);
            maxY = Mathf.Max(maxY, point.y);
        }

        float axisSpan = horizontal ? maxX - minX : maxY - minY;
        float crossSpan = horizontal ? maxY - minY : maxX - minX;
        float radius = Mathf.Min(axisSpan, crossSpan) * 0.5f * roundness;
        if (radius <= 0.000001f) return;

        float epsilon = Mathf.Max(Mathf.Abs(axisLength) * 0.00001f, 0.00001f);
        _dashCornerRadii.Clear();
        for (int i = 0; i < contour.Count; i++)
        {
            float coordinate = horizontal ? contour[i].x : contour[i].y;
            bool onCut = Mathf.Abs(coordinate - min) <= epsilon || Mathf.Abs(coordinate - max) <= epsilon;
            _dashCornerRadii.Add(onCut ? radius : 0f);
        }

        BuildRoundedPolygon(contour, _dashCornerRadii, Mathf.Max(2, GetDashCornerSegments()), _roundedDash);
        contour.Clear();
        contour.AddRange(_roundedDash);
    }

    private static int CopySingleContour(List<Vector2> source, List<List<Vector2>> output)
    {
        List<Vector2> contour = GetContour(output, 0);
        contour.AddRange(source);
        ClearContoursAfter(output, 1);
        return 1;
    }

    private static List<Vector2> GetContour(List<List<Vector2>> output, int index)
    {
        while (output.Count <= index) output.Add(new List<Vector2>(16));
        output[index].Clear();
        return output[index];
    }

    private static int ClearContours(List<List<Vector2>> output)
    {
        ClearContoursAfter(output, 0);
        return 0;
    }

    private static void ClearContoursAfter(List<List<Vector2>> output, int activeCount)
    {
        for (int i = activeCount; i < output.Count; i++) output[i].Clear();
    }

    #endregion
}
