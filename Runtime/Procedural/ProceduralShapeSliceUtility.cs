using UnityEngine;

internal static class ProceduralShapeSliceUtility
{
    #region Methods

    internal static void ClampBorderPair(ref float first, ref float second, float size)
    {
        if (size <= 0f) return;
        float total = first + second;
        if (total <= size || total <= 0f) return;
        float scale = size / total;
        first *= scale;
        second *= scale;
    }

    internal static float MapSlicedAxis(
        float normalized,
        float sourceSize,
        float destinationSize,
        float firstBorder,
        float secondBorder,
        bool slicing)
    {
        if (!slicing || sourceSize <= 0.0001f)
            return normalized * destinationSize;

        ClampBorderPair(ref firstBorder, ref secondBorder, sourceSize);
        GetScaledBorders(destinationSize, firstBorder, secondBorder, out float destinationFirst, out float destinationSecond);
        float sourcePosition = normalized * sourceSize;
        float sourceCenter = Mathf.Max(0f, sourceSize - firstBorder - secondBorder);
        float destinationCenter = Mathf.Max(0f, destinationSize - destinationFirst - destinationSecond);

        if (firstBorder > 0f && sourcePosition <= firstBorder)
            return sourcePosition / firstBorder * destinationFirst;
        if (secondBorder > 0f && sourcePosition >= sourceSize - secondBorder)
            return destinationSize - (sourceSize - sourcePosition) / secondBorder * destinationSecond;
        if (sourceCenter <= 0.0001f)
            return normalized * destinationSize;
        return destinationFirst + (sourcePosition - firstBorder) / sourceCenter * destinationCenter;
    }

    internal static float UnmapSlicedAxis(
        float destinationPosition,
        float sourceSize,
        float destinationSize,
        float firstBorder,
        float secondBorder,
        bool slicing)
    {
        if (!slicing || sourceSize <= 0.0001f || destinationSize <= 0.0001f)
            return destinationSize <= 0.0001f ? 0f : destinationPosition / destinationSize;

        ClampBorderPair(ref firstBorder, ref secondBorder, sourceSize);
        GetScaledBorders(destinationSize, firstBorder, secondBorder, out float destinationFirst, out float destinationSecond);
        float sourceCenter = Mathf.Max(0f, sourceSize - firstBorder - secondBorder);
        float destinationCenter = Mathf.Max(0f, destinationSize - destinationFirst - destinationSecond);

        if (destinationFirst > 0f && destinationPosition <= destinationFirst)
            return destinationPosition / destinationFirst * firstBorder / sourceSize;
        if (destinationSecond > 0f && destinationPosition >= destinationSize - destinationSecond)
            return (sourceSize - (destinationSize - destinationPosition) / destinationSecond * secondBorder) / sourceSize;
        if (destinationCenter <= 0.0001f || sourceCenter <= 0.0001f)
            return destinationPosition / destinationSize;
        return (firstBorder + (destinationPosition - destinationFirst) / destinationCenter * sourceCenter) / sourceSize;
    }

    internal static float MapCenterBandAxis(
        float normalized,
        float sourceSize,
        float destinationSize,
        float centerPixels)
    {
        if (sourceSize <= 0.0001f)
            return normalized * destinationSize;

        centerPixels = Mathf.Clamp(centerPixels, 0f, sourceSize);
        if (centerPixels <= 0.0001f)
            return normalized * destinationSize;

        float sourcePosition = normalized * sourceSize;
        float sourceOuter = (sourceSize - centerPixels) * 0.5f;
        float destinationCenter = Mathf.Min(centerPixels, destinationSize);
        float destinationOuter = Mathf.Max(0f, destinationSize - destinationCenter) * 0.5f;

        if (sourcePosition < sourceOuter)
            return sourceOuter <= 0.0001f ? 0f : sourcePosition / sourceOuter * destinationOuter;
        if (sourcePosition > sourceSize - sourceOuter)
            return destinationSize - (sourceSize - sourcePosition) / sourceOuter * destinationOuter;
        return destinationOuter + (sourcePosition - sourceOuter) / centerPixels * destinationCenter;
    }

    internal static float UnmapCenterBandAxis(
        float destinationPosition,
        float sourceSize,
        float destinationSize,
        float centerPixels)
    {
        if (sourceSize <= 0.0001f || destinationSize <= 0.0001f)
            return destinationSize <= 0.0001f ? 0f : destinationPosition / destinationSize;

        centerPixels = Mathf.Clamp(centerPixels, 0f, sourceSize);
        if (centerPixels <= 0.0001f)
            return destinationPosition / destinationSize;

        float sourceOuter = (sourceSize - centerPixels) * 0.5f;
        float destinationCenter = Mathf.Min(centerPixels, destinationSize);
        float destinationOuter = Mathf.Max(0f, destinationSize - destinationCenter) * 0.5f;

        // When the destination is narrower than the protected band, both outer
        // spans collapse to the edges and the available width represents the band.
        if (destinationOuter <= 0.0001f)
            return (sourceOuter + destinationPosition / destinationSize * centerPixels) / sourceSize;
        if (destinationPosition < destinationOuter)
            return destinationPosition / destinationOuter * sourceOuter / sourceSize;
        if (destinationPosition > destinationSize - destinationOuter)
            return (sourceSize - (destinationSize - destinationPosition) / destinationOuter * sourceOuter) / sourceSize;
        return (sourceOuter + (destinationPosition - destinationOuter) / destinationCenter * centerPixels) / sourceSize;
    }

    internal static void GetScaledBorders(float destinationSize, float first, float second, out float scaledFirst, out float scaledSecond)
    {
        float total = first + second;
        float scale = total > destinationSize && total > 0f ? destinationSize / total : 1f;
        scaledFirst = first * scale;
        scaledSecond = second * scale;
    }

    #endregion
}
