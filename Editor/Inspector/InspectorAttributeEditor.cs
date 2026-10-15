using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zeraveene.Shared.Inspector
{
    // Draws serialized fields and [Button] methods honoring the attributes in InspectorAttributes.cs.
    public class InspectorAttributeEditor : UnityEditor.Editor
    {
        #region Types
        private abstract class Node
        {
        }

        private abstract class MemberNode : Node
        {
            public int Order;
            public int Index;
            public GroupAttribute Group;
            public ConditionAttribute[] Conditions;
        }

        private sealed class FieldNode : MemberNode
        {
            public string PropertyPath;
            public bool ReadOnly;
            public bool Inline;
            public InfoBoxAttribute[] InfoBoxes;
        }

        private sealed class ButtonNode : MemberNode
        {
            public MethodInfo Method;
            public string Label;
            public string ButtonGroup;
        }

        private sealed class ButtonRowNode : Node
        {
            public readonly List<ButtonNode> Buttons = new List<ButtonNode>();
        }

        private sealed class GroupNode : Node
        {
            public string Name;
            public bool IsFoldout;
            public bool DefaultExpanded;
            public string StateKey;
            public readonly List<Node> Children = new List<Node>();
        }
        #endregion

        #region Constants
        private const BindingFlags AllDeclared =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        #endregion

        #region Runtime Data
        private static readonly Dictionary<(Type, string), MemberInfo> MemberCache = new Dictionary<(Type, string), MemberInfo>();
        private static readonly HashSet<string> ReportedMissingMembers = new HashSet<string>();
        private static GUIStyle _groupFoldoutStyle;

        private List<Node> _rootNodes;
        private bool _hasScriptField;
        private ButtonNode _pendingButton;
        private readonly Dictionary<string, UnityEditor.Editor> _inlineEditors = new Dictionary<string, UnityEditor.Editor>();
        #endregion

        #region Properties
        protected bool DrawScriptField { get; set; } = true;

        private static GUIStyle GroupFoldoutStyle =>
            _groupFoldoutStyle ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        #endregion

        #region Unity Callbacks
        protected virtual void OnEnable()
        {
        }

        protected virtual void OnDisable()
        {
            foreach (UnityEditor.Editor editor in _inlineEditors.Values)
            {
                if (editor != null)
                    DestroyImmediate(editor);
            }
            _inlineEditors.Clear();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (_rootNodes == null)
                _rootNodes = BuildNodes();

            if (_hasScriptField && DrawScriptField)
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            }

            DrawNodes(_rootNodes);
            serializedObject.ApplyModifiedProperties();

            if (_pendingButton != null)
            {
                ButtonNode button = _pendingButton;
                _pendingButton = null;
                InvokeButton(button);
                GUIUtility.ExitGUI();
            }
        }
        #endregion

        #region Overridable
        // Lets a subclass move members (e.g. base-class fields) into a group.
        protected virtual GroupAttribute GetGroupOverride(string propertyPath) => null;
        #endregion

        #region Build
        private List<Node> BuildNodes()
        {
            Type targetType = target.GetType();
            var members = new List<MemberNode>();

            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script")
                {
                    _hasScriptField = true;
                    continue;
                }

                FieldInfo field = FindField(targetType, iterator.name);
                var node = new FieldNode
                {
                    PropertyPath = iterator.propertyPath,
                    ReadOnly = field != null && field.IsDefined(typeof(ReadOnlyAttribute), true),
                    Inline = field != null && field.IsDefined(typeof(InlineEditorAttribute), true),
                    InfoBoxes = GetAttributes<InfoBoxAttribute>(field)
                };
                InitializeMember(node, field, members.Count, GetGroupOverride(iterator.propertyPath));
                members.Add(node);
            }

            foreach (MethodInfo method in GetButtonMethods(targetType))
            {
                ButtonAttribute button = method.GetCustomAttribute<ButtonAttribute>(true);
                var node = new ButtonNode
                {
                    Method = method,
                    Label = string.IsNullOrEmpty(button.Name) ? ObjectNames.NicifyVariableName(method.Name) : button.Name,
                    ButtonGroup = method.GetCustomAttribute<ButtonGroupAttribute>(true)?.GroupName
                };
                InitializeMember(node, method, members.Count, null);
                members.Add(node);
            }

            var root = new List<Node>();
            var groups = new Dictionary<string, GroupNode>();
            var rows = new Dictionary<string, ButtonRowNode>();
            foreach (MemberNode member in members.OrderBy(m => m.Order).ThenBy(m => m.Index))
            {
                List<Node> container = root;
                string groupName = member.Group?.GroupName;
                if (groupName != null)
                {
                    if (!groups.TryGetValue(groupName, out GroupNode group))
                    {
                        group = new GroupNode
                        {
                            Name = groupName,
                            IsFoldout = member.Group is FoldoutGroupAttribute,
                            StateKey = $"{targetType.FullName}.{groupName}.Expanded"
                        };
                        groups.Add(groupName, group);
                        root.Add(group);
                    }

                    if (member.Group is FoldoutGroupAttribute foldout && foldout.HasDefinedExpanded)
                        group.DefaultExpanded |= foldout.Expanded;
                    container = group.Children;
                }

                if (member is ButtonNode button && button.ButtonGroup != null)
                {
                    string rowKey = groupName + "/" + button.ButtonGroup;
                    if (!rows.TryGetValue(rowKey, out ButtonRowNode row))
                    {
                        row = new ButtonRowNode();
                        rows.Add(rowKey, row);
                        container.Add(row);
                    }
                    row.Buttons.Add(button);
                    continue;
                }

                container.Add(member);
            }

            return root;
        }

        private static void InitializeMember(MemberNode node, MemberInfo member, int index, GroupAttribute groupOverride)
        {
            node.Index = index;
            node.Order = member?.GetCustomAttribute<PropertyOrderAttribute>(true)?.Order ?? 0;
            node.Group = groupOverride ?? GetAttributes<GroupAttribute>(member).FirstOrDefault();
            node.Conditions = GetAttributes<ConditionAttribute>(member);
        }

        private static T[] GetAttributes<T>(MemberInfo member) where T : Attribute =>
            member == null ? Array.Empty<T>() : member.GetCustomAttributes<T>(true).ToArray();

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, AllDeclared);
                if (field != null)
                    return field;
            }
            return null;
        }

        private static IEnumerable<MethodInfo> GetButtonMethods(Type type)
        {
            var hierarchy = new List<Type>();
            for (Type current = type; current != null && current != typeof(Object); current = current.BaseType)
                hierarchy.Add(current);

            for (int i = hierarchy.Count - 1; i >= 0; i--)
            {
                IEnumerable<MethodInfo> methods = hierarchy[i].GetMethods(AllDeclared)
                    .Where(m => m.IsDefined(typeof(ButtonAttribute), true) && m.GetParameters().Length == 0)
                    .OrderBy(m => m.MetadataToken);
                foreach (MethodInfo method in methods)
                    yield return method;
            }
        }
        #endregion

        #region Draw
        private void DrawNodes(List<Node> nodes)
        {
            foreach (Node node in nodes)
            {
                switch (node)
                {
                    case FieldNode field:
                        DrawField(field);
                        break;
                    case ButtonNode button:
                        if (IsVisible(button))
                            DrawButton(button);
                        break;
                    case ButtonRowNode row:
                        DrawButtonRow(row);
                        break;
                    case GroupNode group:
                        DrawGroup(group);
                        break;
                }
            }
        }

        private void DrawGroup(GroupNode group)
        {
            if (!group.Children.Any(IsNodeVisible))
                return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            if (group.IsFoldout)
            {
                bool expanded = SessionState.GetBool(group.StateKey, group.DefaultExpanded);
                bool newExpanded = EditorGUILayout.Foldout(expanded, group.Name, true, GroupFoldoutStyle);
                if (newExpanded != expanded)
                    SessionState.SetBool(group.StateKey, newExpanded);
                if (newExpanded)
                {
                    using (new EditorGUI.IndentLevelScope(1))
                        DrawNodes(group.Children);
                }
            }
            else
            {
                EditorGUILayout.LabelField(group.Name, EditorStyles.boldLabel);
                DrawNodes(group.Children);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawField(FieldNode node)
        {
            if (!IsVisible(node))
                return;

            SerializedProperty property = serializedObject.FindProperty(node.PropertyPath);
            if (property == null)
                return;

            foreach (InfoBoxAttribute infoBox in node.InfoBoxes)
            {
                if (infoBox.VisibleIf == null || EvaluateCondition(infoBox.VisibleIf) != false)
                    EditorGUILayout.HelpBox(infoBox.Message, ToMessageType(infoBox.MessageType));
            }

            using (new EditorGUI.DisabledScope(node.ReadOnly || !IsEnabled(node)))
                EditorGUILayout.PropertyField(property, true);

            if (node.Inline)
                DrawInlineEditor(node, property);
        }

        private void DrawInlineEditor(FieldNode node, SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference
                || property.hasMultipleDifferentValues
                || property.objectReferenceValue == null)
                return;

            string stateKey = $"{target.GetType().FullName}.{node.PropertyPath}.InlineExpanded";
            bool expanded = SessionState.GetBool(stateKey, false);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            bool newExpanded = EditorGUILayout.Foldout(expanded, property.objectReferenceValue.name, true);
            if (newExpanded != expanded)
                SessionState.SetBool(stateKey, newExpanded);

            if (newExpanded)
            {
                using (new EditorGUI.IndentLevelScope(1))
                {
                    _inlineEditors.TryGetValue(node.PropertyPath, out UnityEditor.Editor editor);
                    CreateCachedEditor(property.objectReferenceValue, null, ref editor);
                    _inlineEditors[node.PropertyPath] = editor;
                    if (editor is InspectorAttributeEditor attributeEditor)
                        attributeEditor.DrawScriptField = false;
                    editor.OnInspectorGUI();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawButtonRow(ButtonRowNode row)
        {
            if (!row.Buttons.Any(IsVisible))
                return;

            EditorGUILayout.BeginHorizontal();
            foreach (ButtonNode button in row.Buttons)
            {
                if (IsVisible(button))
                    DrawButton(button);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawButton(ButtonNode button)
        {
            using (new EditorGUI.DisabledScope(!IsEnabled(button)))
            {
                Rect rect = EditorGUI.IndentedRect(EditorGUILayout.GetControlRect());
                if (GUI.Button(rect, button.Label))
                    _pendingButton = button;
            }
        }

        private static MessageType ToMessageType(InfoMessageType type)
        {
            switch (type)
            {
                case InfoMessageType.Info: return MessageType.Info;
                case InfoMessageType.Warning: return MessageType.Warning;
                case InfoMessageType.Error: return MessageType.Error;
                default: return MessageType.None;
            }
        }
        #endregion

        #region Buttons
        private void InvokeButton(ButtonNode button)
        {
            if (button.Method.IsStatic)
            {
                Invoke(button.Method, null, null);
                return;
            }

            Undo.RecordObjects(targets, button.Label);
            foreach (Object item in targets)
            {
                Invoke(button.Method, item, item);
                if (item == null)
                    continue;
                EditorUtility.SetDirty(item);
                if (PrefabUtility.IsPartOfPrefabInstance(item))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }

            serializedObject.Update();
            Repaint();
        }

        private static void Invoke(MethodInfo method, object instance, Object context)
        {
            try
            {
                method.Invoke(instance, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is ExitGUIException)
            {
                throw exception.InnerException;
            }
            catch (TargetInvocationException exception)
            {
                Debug.LogException(exception.InnerException ?? exception, context);
            }
        }
        #endregion

        #region Conditions
        private bool IsNodeVisible(Node node)
        {
            switch (node)
            {
                case MemberNode member: return IsVisible(member);
                case ButtonRowNode row: return row.Buttons.Any(IsVisible);
                case GroupNode group: return group.Children.Any(IsNodeVisible);
                default: return true;
            }
        }

        private bool IsVisible(MemberNode node)
        {
            foreach (ConditionAttribute condition in node.Conditions)
            {
                if (condition is ShowIfAttribute && EvaluateCondition(condition.MemberName) == false)
                    return false;
                if (condition is HideIfAttribute && EvaluateCondition(condition.MemberName) == true)
                    return false;
            }
            return true;
        }

        private bool IsEnabled(MemberNode node)
        {
            foreach (ConditionAttribute condition in node.Conditions)
            {
                if (condition is EnableIfAttribute && EvaluateCondition(condition.MemberName) == false)
                    return false;
                if (condition is DisableIfAttribute && EvaluateCondition(condition.MemberName) == true)
                    return false;
            }
            return true;
        }

        // Returns null when the member cannot be resolved, so the condition is ignored.
        private bool? EvaluateCondition(string memberName)
        {
            Type type = target.GetType();
            MemberInfo member = FindMember(type, memberName);
            if (member == null)
            {
                if (ReportedMissingMembers.Add(type.FullName + "." + memberName))
                    Debug.LogWarning($"Inspector condition member '{memberName}' was not found on {type.Name}.", target);
                return null;
            }

            try
            {
                object instance = IsStatic(member) ? null : target;
                switch (member)
                {
                    case FieldInfo field: return ToBool(field.GetValue(instance));
                    case PropertyInfo property: return ToBool(property.GetValue(instance));
                    case MethodInfo method: return ToBool(method.Invoke(instance, null));
                }
            }
            catch (Exception exception)
            {
                if (ReportedMissingMembers.Add(type.FullName + "." + memberName + ".Exception"))
                    Debug.LogException(exception.InnerException ?? exception, target);
            }
            return null;
        }

        private static MemberInfo FindMember(Type type, string name)
        {
            if (MemberCache.TryGetValue((type, name), out MemberInfo cached))
                return cached;

            MemberInfo result = null;
            for (Type current = type; current != null && result == null; current = current.BaseType)
            {
                result = current.GetField(name, AllDeclared);
                if (result != null)
                    break;

                PropertyInfo property = current.GetProperty(name, AllDeclared);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    result = property;
                else
                    result = current.GetMethod(name, AllDeclared, null, Type.EmptyTypes, null);
            }

            MemberCache[(type, name)] = result;
            return result;
        }

        private static bool IsStatic(MemberInfo member)
        {
            switch (member)
            {
                case FieldInfo field: return field.IsStatic;
                case PropertyInfo property: return property.GetMethod.IsStatic;
                case MethodInfo method: return method.IsStatic;
                default: return false;
            }
        }

        private static bool ToBool(object value)
        {
            switch (value)
            {
                case bool flag: return flag;
                case Object unityObject: return unityObject != null;
                case null: return false;
            }

            Type type = value.GetType();
            return !type.IsValueType || !value.Equals(Activator.CreateInstance(type));
        }
        #endregion
    }
}
