using System.Collections.Generic;
using UnityEngine;

internal static class ProceduralShapeGeometryUtility
{
    private const float Epsilon = 0.000001f;
    private static readonly List<bool> _cleanRemoved = new List<bool>(64);

    internal static void BuildRoundedPolygon(IList<Vector2> points, IList<float> radii, int segments, List<Vector2> output)
    {
        output.Clear();
        if (points == null || radii == null || points.Count < 3 || points.Count != radii.Count) return;
        segments = Mathf.Max(2, segments);
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 point = points[i];
            Vector2 previous = points[(i + points.Count - 1) % points.Count] - point;
            Vector2 next = points[(i + 1) % points.Count] - point;
            float previousLength = previous.magnitude;
            float nextLength = next.magnitude;
            float radius = Mathf.Max(0f, radii[i]);
            if (radius <= 0f || previousLength < 0.0001f || nextLength < 0.0001f)
            {
                output.Add(point);
                continue;
            }

            Vector2 previousDirection = previous / previousLength;
            Vector2 nextDirection = next / nextLength;
            float halfAngle = Mathf.Acos(Mathf.Clamp(Vector2.Dot(previousDirection, nextDirection), -1f, 1f)) * 0.5f;
            float sine = Mathf.Sin(halfAngle);
            float tangentFactor = Mathf.Tan(halfAngle);
            Vector2 bisector = previousDirection + nextDirection;
            if (sine < 0.0001f || tangentFactor < 0.0001f || bisector.sqrMagnitude < 0.0000001f)
            {
                output.Add(point);
                continue;
            }

            float tangent = Mathf.Min(radius / tangentFactor, previousLength * 0.5f, nextLength * 0.5f);
            float effectiveRadius = tangent * tangentFactor;
            Vector2 center = point + bisector.normalized * (effectiveRadius / sine);
            Vector2 startPoint = point + previousDirection * tangent;
            Vector2 endPoint = point + nextDirection * tangent;
            float start = Mathf.Atan2(startPoint.y - center.y, startPoint.x - center.x);
            float end = Mathf.Atan2(endPoint.y - center.y, endPoint.x - center.x);
            float delta = Mathf.Atan2(Mathf.Sin(end - start), Mathf.Cos(end - start));
            for (int segment = 0; segment <= segments; segment++)
            {
                float angle = start + delta * segment / segments;
                output.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * effectiveRadius);
            }
        }
    }

    internal static bool Triangulate(IList<Vector2> points, List<int> triangles, List<int> indices)
    {
        triangles.Clear();
        indices.Clear();
        float area = Area(points);
        if (points == null || points.Count < 3 || Mathf.Abs(area) < 0.000001f) return false;
        bool ccw = area > 0f;
        if (TryTriangulateConvex(points, ccw, triangles)) return true;
        triangles.Clear();
        for (int i = 0; i < points.Count; i++) indices.Add(i);
        int guard = points.Count * points.Count;
        while (indices.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int a = indices[(i + indices.Count - 1) % indices.Count];
                int b = indices[i];
                int c = indices[(i + 1) % indices.Count];
                float cross = Cross(points[b] - points[a], points[c] - points[b]);
                if (ccw ? cross <= 0.000001f : cross >= -0.000001f) continue;
                bool blocked = false;
                for (int j = 0; j < indices.Count; j++)
                {
                    int p = indices[j];
                    if (p != a && p != b && p != c && InTriangle(points[p], points[a], points[b], points[c]))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (blocked) continue;
                AddTriangle(triangles, a, b, c, ccw);
                indices.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) return false;
        }
        if (indices.Count != 3) return false;
        AddTriangle(triangles, indices[0], indices[1], indices[2], ccw);
        return true;
    }

    // Strictly convex and wound once. Turns within float noise of collinear fail, so callers fall back to the exact path.
    internal static bool IsConvex(IList<Vector2> points, bool ccw)
    {
        return IsConvex(points, ccw, out _, out _);
    }

    // Emits exactly the triangles the ear clipper produces for a convex polygon: it always clips the ear at
    // index 0, giving a fan around the last point. Each ear is still checked the way the ear clipper checks it;
    // any doubt returns false and the caller runs the full ear clipper.
    internal static bool TryTriangulateConvex(IList<Vector2> points, bool ccw, List<int> triangles)
    {
        triangles.Clear();
        float scale, span;
        if (!IsConvex(points, ccw, out scale, out span)) return false;
        int count = points.Count;
        int last = count - 1;
        Vector2 a = points[last];
        for (int k = 0; k < count - 3; k++)
        {
            Vector2 b = points[k], c = points[k + 1];
            Vector2 ab = b - a, bc = c - b;
            float cross = Cross(ab, bc);
            float earMargin = Epsilon * 3f + CrossNoise(ab, bc, scale);
            if (!(ccw ? cross > earMargin : cross < -earMargin)) return Fail(triangles);

            int near = k + 2, far = last - 1;
            if (InTriangle(points[near], a, b, c)) return Fail(triangles);
            if (far != near && InTriangle(points[far], a, b, c)) return Fail(triangles);
            if (far - near > 1)
            {
                // Distance to the diagonal is concave along the convex chain, so the points between the two
                // neighbours lie farther outside the ear than the nearer neighbour. Verify exactly when close.
                Vector2 ca = a - c;
                float zMargin = Epsilon * 3f + 2f * CrossNoise(ca, new Vector2(span, 0f), scale);
                float zNear = Cross(ca, points[near] - c), zFar = Cross(ca, points[far] - c);
                if (!ccw) { zNear = -zNear; zFar = -zFar; }
                if (!(zNear < -zMargin && zFar < -zMargin))
                    for (int p = near + 1; p < far; p++)
                        if (InTriangle(points[p], a, b, c)) return Fail(triangles);
            }
            AddTriangle(triangles, last, k, k + 1, ccw);
        }
        AddTriangle(triangles, count - 3, count - 2, count - 1, ccw);
        return true;
    }

    internal static bool Contains(Vector2 point, IList<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[j], b = polygon[i], edge = b - a;
            float t = edge.sqrMagnitude < 0.0000001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
            if ((point - (a + edge * t)).sqrMagnitude < 0.000001f) return true;
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    // Same result as removing in place while walking down from the last point, without the O(n) RemoveAt:
    // below i the list is untouched, above i "next" is the nearest kept point and index 0 wraps to the highest kept one.
    internal static void Clean(List<Vector2> points)
    {
        bool changed;
        do
        {
            changed = false;
            int count = points.Count;
            int remaining = count;
            int next = 0;
            int highestKept = -1;
            _cleanRemoved.Clear();
            for (int i = 0; i < count; i++) _cleanRemoved.Add(false);
            for (int i = count - 1; i >= 0 && remaining > 2; i--)
            {
                Vector2 previous = points[i > 0 ? i - 1 : highestKept];
                Vector2 current = points[i];
                Vector2 incoming = current - previous;
                Vector2 outgoing = points[next] - current;
                if (incoming.sqrMagnitude < 0.0000001f ||
                    (Mathf.Abs(Cross(incoming, outgoing)) < 0.000001f && Vector2.Dot(incoming, outgoing) >= 0f))
                {
                    _cleanRemoved[i] = true;
                    remaining--;
                    changed = true;
                    continue;
                }
                next = i;
                if (highestKept < 0) highestKept = i;
            }
            if (!changed) return;
            int write = 0;
            for (int read = 0; read < count; read++)
                if (!_cleanRemoved[read]) points[write++] = points[read];
            points.RemoveRange(write, count - write);
        } while (points.Count > 2);
    }

    internal static bool IsSimple(IList<Vector2> points)
    {
        if (points == null || points.Count < 3) return false;
        float area = Area(points);
        if (Mathf.Abs(area) < 0.000001f) return false;
        if (IsConvex(points, area > 0f)) return true;
        for (int i = 0; i < points.Count; i++)
        for (int j = i + 1; j < points.Count; j++)
        {
            if (j == (i + 1) % points.Count || (j + 1) % points.Count == i) continue;
            if (Intersects(points[i], points[(i + 1) % points.Count], points[j], points[(j + 1) % points.Count])) return false;
        }
        return true;
    }

    internal static void Offset(IList<Vector2> source, float inwardDistance, List<Vector2> output)
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

    internal static bool IsUsableInset(IList<Vector2> outer, IList<Vector2> inset)
    {
        if (!IsSimple(inset)) return false;
        float outerArea = Area(outer), insetArea = Area(inset);
        return outerArea * insetArea > 0f && Mathf.Abs(insetArea) < Mathf.Abs(outerArea);
    }

    internal static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static float Area(IList<Vector2> points)
    {
        if (points == null) return 0f;
        float area = 0f;
        for (int i = 0; i < points.Count; i++) area += Cross(points[i], points[(i + 1) % points.Count]);
        return area * 0.5f;
    }

    private static bool IsConvex(IList<Vector2> points, bool ccw, out float scale, out float span)
    {
        scale = 0f;
        span = 0f;
        int count = points == null ? 0 : points.Count;
        if (count < 3) return false;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            Vector2 point = points[i];
            minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
        }
        scale = Mathf.Max(Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX)), Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY)));
        span = (maxX - minX) + (maxY - minY);

        // Edge directions of a polygon wound once change x and y sign at most twice each.
        int xFlips = 0, yFlips = 0, firstX = 0, firstY = 0, lastX = 0, lastY = 0;
        Vector2 previous = points[count - 2];
        Vector2 current = points[count - 1];
        for (int i = 0; i < count; i++)
        {
            Vector2 next = points[i];
            Vector2 incoming = current - previous;
            Vector2 outgoing = next - current;
            float cross = Cross(incoming, outgoing);
            float margin = Epsilon + CrossNoise(incoming, outgoing, scale);
            if (!(ccw ? cross > margin : cross < -margin)) return false;
            CountFlip(outgoing.x, ref firstX, ref lastX, ref xFlips);
            CountFlip(outgoing.y, ref firstY, ref lastY, ref yFlips);
            previous = current;
            current = next;
        }
        if (lastX != firstX) xFlips++;
        if (lastY != firstY) yFlips++;
        return xFlips <= 2 && yFlips <= 2;
    }

    private static void CountFlip(float value, ref int first, ref int last, ref int flips)
    {
        int sign = value > 0f ? 1 : value < 0f ? -1 : 0;
        if (sign == 0) return;
        if (first == 0) first = sign;
        else if (sign != last) flips++;
        last = sign;
    }

    // Generous bound on the float error of Cross(u, v) for differences of coordinates up to `scale`.
    private static float CrossNoise(Vector2 u, Vector2 v, float scale)
    {
        float lu = Mathf.Abs(u.x) + Mathf.Abs(u.y);
        float lv = Mathf.Abs(v.x) + Mathf.Abs(v.y);
        return 0.0000004f * (scale * (lu + lv) + lu * lv);
    }

    private static bool Fail(List<int> triangles)
    {
        triangles.Clear();
        return false;
    }

    private static void AddTriangle(List<int> triangles, int a, int b, int c, bool ccw)
    {
        triangles.Add(a);
        triangles.Add(ccw ? b : c);
        triangles.Add(ccw ? c : b);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float x = Cross(b - a, p - a), y = Cross(c - b, p - b), z = Cross(a - c, p - c);
        return !(x < -0.000001f || y < -0.000001f || z < -0.000001f) ||
               !(x > 0.000001f || y > 0.000001f || z > 0.000001f);
    }

    private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return Cross(b - a, c - a) * Cross(b - a, d - a) < -0.0000001f &&
               Cross(d - c, a - c) * Cross(d - c, b - c) < -0.0000001f;
    }

    private static Vector2 Normal(Vector2 edge, bool ccw)
    {
        if (edge.sqrMagnitude < 0.0000001f) return Vector2.zero;
        edge.Normalize();
        return ccw ? new Vector2(-edge.y, edge.x) : new Vector2(edge.y, -edge.x);
    }
}
