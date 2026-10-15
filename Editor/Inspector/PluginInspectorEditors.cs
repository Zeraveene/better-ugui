using UnityEditor;

namespace Zeraveene.Shared.Inspector
{
    [CustomEditor(typeof(UIBase), true)]
    [CanEditMultipleObjects]
    internal sealed class UIBaseEditor : InspectorAttributeEditor
    {
    }

    [CustomEditor(typeof(UITransitionPreset))]
    [CanEditMultipleObjects]
    internal sealed class UITransitionPresetEditor : InspectorAttributeEditor
    {
    }

    [CustomEditor(typeof(UIViewSequence))]
    [CanEditMultipleObjects]
    internal sealed class UIViewSequenceEditor : InspectorAttributeEditor
    {
    }

    [CustomEditor(typeof(ProceduralShapeOutline))]
    [CanEditMultipleObjects]
    internal sealed class ProceduralShapeOutlineEditor : InspectorAttributeEditor
    {
    }
}
