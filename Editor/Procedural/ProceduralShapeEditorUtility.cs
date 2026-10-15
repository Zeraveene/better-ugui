using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class ProceduralShapeEditorUtility
{
    private const string ToShape = "Turn into Procedural Shape";
    private const string ToCustom = "Turn into Procedural Shape Custom";
    private const string ToImage = "Turn into Procedural Shape Image";

    [MenuItem("CONTEXT/Image/" + ToImage)]
    private static void ImageToShapeImage(MenuCommand command) => ConvertTo<ProceduralShapeImage>((Graphic)command.context);

    [MenuItem("CONTEXT/Image/" + ToImage, true)]
    private static bool ValidateImageToShapeImage(MenuCommand command) => command.context is Image && command.context is not ProceduralShapeImage;

    [MenuItem("CONTEXT/ProceduralShape/" + ToCustom)]
    private static void ShapeToCustom(MenuCommand command) => ConvertTo<ProceduralShapeCustom>((Graphic)command.context);

    [MenuItem("CONTEXT/ProceduralShape/" + ToImage)]
    private static void ShapeToImage(MenuCommand command) => ConvertTo<ProceduralShapeImage>((Graphic)command.context);

    [MenuItem("CONTEXT/ProceduralShapeCustom/" + ToShape)]
    private static void CustomToShape(MenuCommand command) => ConvertTo<ProceduralShape>((Graphic)command.context);

    [MenuItem("CONTEXT/ProceduralShapeCustom/" + ToImage)]
    private static void CustomToImage(MenuCommand command) => ConvertTo<ProceduralShapeImage>((Graphic)command.context);

    [MenuItem("CONTEXT/ProceduralShapeImage/" + ToShape)]
    private static void ImageToShape(MenuCommand command) => ConvertTo<ProceduralShape>((Graphic)command.context);

    [MenuItem("CONTEXT/ProceduralShapeImage/" + ToCustom)]
    private static void ImageToCustom(MenuCommand command) => ConvertTo<ProceduralShapeCustom>((Graphic)command.context);

    public static ProceduralShapeOutline AddOutline(Graphic source)
    {
        if (source == null) return null;
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Add Procedural Shape Outline");

        Transform parent = source.transform;
        string objectName = GameObjectUtility.GetUniqueNameForSibling(parent, "Outline");
        var child = new GameObject(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(child, "Add Procedural Shape Outline");
        child.layer = source.gameObject.layer;
        RectTransform rectTransform = (RectTransform)child.transform;
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;

        ProceduralShapeOutline outline = Undo.AddComponent<ProceduralShapeOutline>(child);
        outline.raycastTarget = false;
        outline.ThicknessPixels = 1f;
        if (source is ProceduralShapeGraphic shape) outline.Source = shape;
        else if (source is ProceduralShapeImage image) outline.ImageSource = image;
        Undo.AddComponent<UIGradientEffect>(child);

        Selection.activeGameObject = child;
        EditorUtility.SetDirty(child);
        PrefabUtility.RecordPrefabInstancePropertyModifications(rectTransform);
        Undo.CollapseUndoOperations(group);
        return outline;
    }

    internal static T ConvertTo<T>(Graphic source) where T : Graphic
    {
        if (source == null || source is T) return source as T;
        GameObject gameObject = source.gameObject;
        if (!CanRestoreDependencies(gameObject, source.GetType(), typeof(T), out string dependencyError))
        {
            EditorUtility.DisplayDialog("Cannot Convert Procedural Shape", dependencyError, "OK");
            return null;
        }
        TransferData data = TransferData.Capture(source);
        List<EffectData> effects = CaptureDependencies(gameObject, source);
        List<ReferenceData> references = CaptureLocalReferences(gameObject, source);
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"Convert {source.GetType().Name} to {typeof(T).Name}");

        for (int i = effects.Count - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(effects[i].Component);
        Undo.DestroyObjectImmediate(source);
        T target = Undo.AddComponent<T>(gameObject);
        if (target == null)
        {
            Debug.LogError($"Failed to add {typeof(T).Name} to {gameObject.name}. Undo the conversion to restore the original component.", gameObject);
            Undo.CollapseUndoOperations(group);
            return null;
        }
        data.Apply(target);
        RestoreEffects(gameObject, effects);
        RestoreLocalReferences(references, target);

        EditorUtility.SetDirty(gameObject);
        PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        Selection.activeGameObject = gameObject;
        Undo.CollapseUndoOperations(group);
        return target;
    }

    private static List<EffectData> CaptureDependencies(GameObject gameObject, Graphic source)
    {
        Component[] components = gameObject.GetComponents<Component>();
        var result = new List<EffectData>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component == source || !Requires(component.GetType(), source.GetType())) continue;
            result.Add(new EffectData(component, EditorJsonUtility.ToJson(component), CaptureReferencePaths(component, source)));
        }
        return result;
    }

    private static void RestoreEffects(GameObject gameObject, List<EffectData> effects)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            Component component = Undo.AddComponent(gameObject, effects[i].Type);
            EditorJsonUtility.FromJsonOverwrite(effects[i].Json, component);
            for (int pathIndex = 0; pathIndex < effects[i].SourceReferencePaths.Count; pathIndex++)
            {
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.FindProperty(effects[i].SourceReferencePaths[pathIndex]);
                if (property == null) continue;
                property.objectReferenceValue = gameObject.GetComponent<Graphic>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(component);
        }
    }

    private static bool CanRestoreDependencies(GameObject gameObject, Type sourceType, Type targetType, out string error)
    {
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component.GetType() == sourceType) continue;
            Type[] requirements = GetRequiredTypes(component.GetType());
            for (int requirementIndex = 0; requirementIndex < requirements.Length; requirementIndex++)
            {
                Type requirement = requirements[requirementIndex];
                if (requirement == null || !requirement.IsAssignableFrom(sourceType) || requirement.IsAssignableFrom(targetType)) continue;
                error = $"{component.GetType().Name} requires {requirement.Name}, but {targetType.Name} does not provide it.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool Requires(Type componentType, Type sourceType)
    {
        Type[] requirements = GetRequiredTypes(componentType);
        for (int i = 0; i < requirements.Length; i++)
            if (requirements[i] != null && requirements[i].IsAssignableFrom(sourceType)) return true;
        return false;
    }

    private static Type[] GetRequiredTypes(Type componentType)
    {
        object[] attributes = componentType.GetCustomAttributes(typeof(RequireComponent), true);
        var result = new List<Type>(attributes.Length * 3);
        for (int i = 0; i < attributes.Length; i++)
        {
            var requirement = (RequireComponent)attributes[i];
            if (requirement.m_Type0 != null) result.Add(requirement.m_Type0);
            if (requirement.m_Type1 != null) result.Add(requirement.m_Type1);
            if (requirement.m_Type2 != null) result.Add(requirement.m_Type2);
        }
        return result.ToArray();
    }

    private static List<string> CaptureReferencePaths(Component component, UnityEngine.Object source)
    {
        var paths = new List<string>();
        SerializedObject serialized = new SerializedObject(component);
        SerializedProperty iterator = serialized.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == source)
                paths.Add(iterator.propertyPath);
        }
        return paths;
    }

    private static List<ReferenceData> CaptureLocalReferences(GameObject gameObject, Graphic source)
    {
        Component[] components = gameObject.GetComponents<Component>();
        var references = new List<ReferenceData>();
        for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            Component component = components[componentIndex];
            if (component == null || component == source || component is BaseMeshEffect) continue;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == source)
                    references.Add(new ReferenceData(component, iterator.propertyPath));
            }
        }
        return references;
    }

    private static void RestoreLocalReferences(List<ReferenceData> references, Graphic target)
    {
        for (int i = 0; i < references.Count; i++)
        {
            if (references[i].Component == null) continue;
            SerializedObject serialized = new SerializedObject(references[i].Component);
            SerializedProperty property = serialized.FindProperty(references[i].PropertyPath);
            if (property == null) continue;
            Undo.RecordObject(references[i].Component, "Update Procedural Shape Reference");
            property.objectReferenceValue = target;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(references[i].Component);
        }
    }

    private readonly struct EffectData
    {
        public readonly Component Component;
        public readonly Type Type;
        public readonly string Json;
        public readonly List<string> SourceReferencePaths;

        public EffectData(Component component, string json, List<string> sourceReferencePaths)
        {
            Component = component;
            Type = component.GetType();
            Json = json;
            SourceReferencePaths = sourceReferencePaths;
        }
    }

    private readonly struct ReferenceData
    {
        public readonly Component Component;
        public readonly string PropertyPath;

        public ReferenceData(Component component, string propertyPath)
        {
            Component = component;
            PropertyPath = propertyPath;
        }
    }

    private sealed class TransferData
    {
        private Color _color;
        private Material _material;
        private bool _raycastTarget;
        private Vector4 _raycastPadding;
        private bool _maskable;

        private Sprite _sprite;
        private Sprite _overrideSprite;
        private Image.Type _imageType = Image.Type.Simple;
        private bool _preserveAspect;
        private bool _fillCenter = true;
        private Image.FillMethod _fillMethod;
        private float _fillAmount = 1f;
        private bool _fillClockwise = true;
        private int _fillOrigin;
        private float _pixelsPerUnitMultiplier = 1f;
        private float _alphaHitTestMinimumThreshold;

        private ProceduralShapeRenderType _renderType;
        private float _borderWidth;
        private float _falloffDistance;
        private ProceduralShapeMeasurementMode _measurementMode;
        private List<ProceduralShapePoint> _points = RectanglePoints();
        private bool _uniformRadius = true;
        private float _cornerRadius;
        private int _cornerSegments = 8;
        private bool _snapPointsToPixels = true;
        private ProceduralShapeMode? _shapeMode;
        private ProceduralShapeTemplate _pointTemplate;
        private bool _hasPointOverrides;
        private bool _sliceHorizontal;
        private bool _sliceVertical;
        private ProceduralShapeHorizontalSliceMode _horizontalSliceMode;
        private float _horizontalCenterPixels;
        private Vector2 _sliceReferenceSize;
        private Vector4 _sliceBorders;

        public static TransferData Capture(Graphic source)
        {
            SerializedObject serializedSource = new SerializedObject(source);
            SerializedProperty materialProperty = serializedSource.FindProperty("m_Material");
            var data = new TransferData
            {
                _color = source.color,
                _material = materialProperty != null ? materialProperty.objectReferenceValue as Material : null,
                _raycastTarget = source.raycastTarget,
                _raycastPadding = source.raycastPadding,
                _maskable = source is MaskableGraphic maskable && maskable.maskable
            };

            if (source is Image image)
            {
                SerializedObject serializedImage = new SerializedObject(image);
                data._sprite = serializedImage.FindProperty("m_Sprite")?.objectReferenceValue as Sprite;
                data._overrideSprite = serializedImage.FindProperty("m_OverrideSprite")?.objectReferenceValue as Sprite;
                data._imageType = image.type == Image.Type.Filled ? Image.Type.Filled : Image.Type.Simple;
                data._preserveAspect = image.preserveAspect;
                data._fillCenter = image.fillCenter;
                data._fillMethod = image.fillMethod;
                data._fillAmount = image.fillAmount;
                data._fillClockwise = image.fillClockwise;
                data._fillOrigin = image.fillOrigin;
                data._pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
                data._alphaHitTestMinimumThreshold = image.alphaHitTestMinimumThreshold;
                data._renderType = image.type == Image.Type.Filled ? ProceduralShapeRenderType.Filled : ProceduralShapeRenderType.Simple;
            }

            if (source is ProceduralShapeGraphic procedural)
            {
                data._renderType = procedural.RenderType;
                data._fillMethod = procedural.FillMethod;
                data._fillAmount = procedural.FillAmount;
                data._fillClockwise = procedural.FillClockwise;
                data._fillOrigin = procedural.FillOrigin;
                data._borderWidth = procedural.BorderWidth;
                data._falloffDistance = procedural.FalloffDistance;
                data._measurementMode = procedural.MeasurementMode;
                data._imageType = procedural.RenderType == ProceduralShapeRenderType.Filled ? Image.Type.Filled : Image.Type.Simple;
            }

            if (source is ProceduralShape shape) data.CaptureShape(shape);
            else if (source is ProceduralShapeCustom custom) data.CaptureCustom(custom);
            else if (source is ProceduralShapeImage shapeImage) data.CaptureImageGeometry(shapeImage);
            return data;
        }

        public void Apply(Graphic target)
        {
            target.color = _color;
            target.material = _material;
            target.raycastTarget = _raycastTarget;
            target.raycastPadding = _raycastPadding;
            if (target is MaskableGraphic maskable) maskable.maskable = _maskable;

            if (target is Image image)
            {
                image.sprite = _sprite;
                image.overrideSprite = _overrideSprite;
                image.type = _imageType;
                image.preserveAspect = _preserveAspect;
                image.fillCenter = _fillCenter;
                image.fillMethod = _fillMethod;
                image.fillAmount = _fillAmount;
                image.fillClockwise = _fillClockwise;
                image.fillOrigin = _fillOrigin;
                image.pixelsPerUnitMultiplier = _pixelsPerUnitMultiplier;
                image.alphaHitTestMinimumThreshold = _alphaHitTestMinimumThreshold;
            }

            if (target is ProceduralShapeGraphic procedural)
            {
                procedural.RenderType = _renderType;
                procedural.FillMethod = _fillMethod;
                procedural.FillAmount = _fillAmount;
                procedural.FillClockwise = _fillClockwise;
                procedural.FillOrigin = _fillOrigin;
                procedural.BorderWidth = _borderWidth;
                procedural.FalloffDistance = _falloffDistance;
                procedural.MeasurementMode = _measurementMode;
            }

            if (target is ProceduralShape shape) ApplyShape(shape);
            else if (target is ProceduralShapeCustom custom) ApplyCustom(custom);
            else if (target is ProceduralShapeImage shapeImage) ApplyImageGeometry(shapeImage);
        }

        private void CaptureShape(ProceduralShape shape)
        {
            _shapeMode = shape.ShapeMode;
            _cornerSegments = shape.CornerSegments;
            _snapPointsToPixels = shape.SnapCornersToPixels;
            if (shape.ShapeMode == ProceduralShapeMode.Circle)
            {
                var perimeter = new List<Vector2>();
                shape.GetRoundedPerimeterLocal(perimeter);
                Rect rect = shape.rectTransform.rect;
                _points = new List<ProceduralShapePoint>(perimeter.Count);
                for (int i = 0; i < perimeter.Count; i++)
                {
                    float x = Mathf.InverseLerp(rect.xMin, rect.xMax, perimeter[i].x);
                    float y = Mathf.InverseLerp(rect.yMin, rect.yMax, perimeter[i].y);
                    _points.Add(new ProceduralShapePoint(new Vector2(x, y)));
                }
                _uniformRadius = false;
                return;
            }
            Vector4 radii = shape.Radii;
            _points = new List<ProceduralShapePoint>
            {
                new ProceduralShapePoint(shape.BottomLeft, radii.x),
                new ProceduralShapePoint(shape.TopLeft, radii.y),
                new ProceduralShapePoint(shape.TopRight, radii.z),
                new ProceduralShapePoint(shape.BottomRight, radii.w)
            };
            _uniformRadius = shape.ShapeMode == ProceduralShapeMode.Uniform;
            _cornerRadius = shape.CornerRadius;
        }

        private void CaptureCustom(ProceduralShapeCustom shape)
        {
            _points = CopyPoints(shape.Points);
            _uniformRadius = shape.UniformRadius;
            _cornerRadius = shape.CornerRadius;
            _cornerSegments = shape.CornerSegments;
            _snapPointsToPixels = shape.SnapPointsToPixels;
            _pointTemplate = shape.PointTemplate;
            _hasPointOverrides = shape.HasPointOverrides;
            CaptureSlicing(shape.SliceHorizontal, shape.SliceVertical, shape.HorizontalSliceMode,
                shape.HorizontalCenterPixels, shape.SliceReferenceSize, shape.SliceBorders);
        }

        private void CaptureImageGeometry(ProceduralShapeImage shape)
        {
            _points = CopyPoints(shape.Points);
            _uniformRadius = shape.UniformRadius;
            _cornerRadius = shape.CornerRadius;
            _cornerSegments = shape.CornerSegments;
            _snapPointsToPixels = shape.SnapPointsToPixels;
            _pointTemplate = shape.PointTemplate;
            _hasPointOverrides = shape.HasPointOverrides;
            _borderWidth = shape.BorderWidth;
            _falloffDistance = shape.FalloffDistance;
            _measurementMode = shape.MeasurementMode;
            CaptureSlicing(shape.SliceHorizontal, shape.SliceVertical, shape.HorizontalSliceMode,
                shape.HorizontalCenterPixels, shape.SliceReferenceSize, shape.SliceBorders);
        }

        private void CaptureSlicing(bool horizontal, bool vertical, ProceduralShapeHorizontalSliceMode mode,
            float center, Vector2 referenceSize, Vector4 borders)
        {
            _sliceHorizontal = horizontal;
            _sliceVertical = vertical;
            _horizontalSliceMode = mode;
            _horizontalCenterPixels = center;
            _sliceReferenceSize = referenceSize;
            _sliceBorders = borders;
        }

        private void ApplyShape(ProceduralShape shape)
        {
            List<ProceduralShapePoint> targetPoints = _points.Count == 4 ? _points : BoundingRectangle(_points);
            shape.ShapeMode = _shapeMode.HasValue && _points.Count == 4 ? _shapeMode.Value : ProceduralShapeMode.NonUniform;
            shape.BottomLeft = targetPoints[0].Position;
            shape.TopLeft = targetPoints[1].Position;
            shape.TopRight = targetPoints[2].Position;
            shape.BottomRight = targetPoints[3].Position;
            shape.Radii = new Vector4(targetPoints[0].Radius, targetPoints[1].Radius, targetPoints[2].Radius, targetPoints[3].Radius);
            if (_uniformRadius && _points.Count == 4) shape.CornerRadius = _cornerRadius;
            shape.CornerSegments = _cornerSegments;
            shape.SnapCornersToPixels = _snapPointsToPixels;
        }

        private void ApplyCustom(ProceduralShapeCustom shape)
        {
            shape.SetPointTemplate(_pointTemplate);
            if (_pointTemplate == null || _hasPointOverrides) shape.SetPoints(_points);
            shape.UniformRadius = _uniformRadius;
            if (_uniformRadius) shape.CornerRadius = _cornerRadius;
            shape.CornerSegments = _cornerSegments;
            shape.SnapPointsToPixels = _snapPointsToPixels;
            shape.SetSliceReferenceSize(_sliceReferenceSize);
            shape.SliceBorders = _sliceBorders;
            shape.HorizontalSliceMode = _horizontalSliceMode;
            shape.HorizontalCenterPixels = _horizontalCenterPixels;
            shape.SliceHorizontal = _sliceHorizontal;
            shape.SliceVertical = _sliceVertical;
        }

        private void ApplyImageGeometry(ProceduralShapeImage shape)
        {
            shape.SetPointTemplate(_pointTemplate);
            if (_pointTemplate == null || _hasPointOverrides) shape.SetPoints(_points);
            shape.UniformRadius = _uniformRadius;
            if (_uniformRadius) shape.CornerRadius = _cornerRadius;
            shape.CornerSegments = _cornerSegments;
            shape.SnapPointsToPixels = _snapPointsToPixels;
            shape.BorderWidth = _borderWidth;
            shape.FalloffDistance = _falloffDistance;
            shape.MeasurementMode = _measurementMode;
            shape.SetSliceReferenceSize(_sliceReferenceSize);
            shape.SliceBorders = _sliceBorders;
            shape.HorizontalSliceMode = _horizontalSliceMode;
            shape.HorizontalCenterPixels = _horizontalCenterPixels;
            shape.SliceHorizontal = _sliceHorizontal;
            shape.SliceVertical = _sliceVertical;
        }

        private static List<ProceduralShapePoint> CopyPoints(IReadOnlyList<ProceduralShapePoint> source)
        {
            var points = new List<ProceduralShapePoint>(source.Count);
            for (int i = 0; i < source.Count; i++) points.Add(new ProceduralShapePoint(source[i].Position, source[i].Radius));
            return points;
        }

        private static List<ProceduralShapePoint> RectanglePoints() => new List<ProceduralShapePoint>
        {
            new ProceduralShapePoint(new Vector2(0f, 0f)),
            new ProceduralShapePoint(new Vector2(0f, 1f)),
            new ProceduralShapePoint(new Vector2(1f, 1f)),
            new ProceduralShapePoint(new Vector2(1f, 0f))
        };

        private static List<ProceduralShapePoint> BoundingRectangle(IReadOnlyList<ProceduralShapePoint> points)
        {
            if (points == null || points.Count == 0) return RectanglePoints();
            Vector2 min = points[0].Position, max = min;
            for (int i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i].Position);
                max = Vector2.Max(max, points[i].Position);
            }
            return new List<ProceduralShapePoint>
            {
                new ProceduralShapePoint(new Vector2(min.x, min.y)),
                new ProceduralShapePoint(new Vector2(min.x, max.y)),
                new ProceduralShapePoint(new Vector2(max.x, max.y)),
                new ProceduralShapePoint(new Vector2(max.x, min.y))
            };
        }
    }
}
