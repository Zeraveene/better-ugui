using System;
using System.Diagnostics;

namespace Zeraveene.Shared.Inspector
{
    // Lightweight Inspector attributes drawn by InspectorAttributeEditor (Editor folder).
    // Only types that have an editor deriving from InspectorAttributeEditor honor them.

    public enum InfoMessageType
    {
        None,
        Info,
        Warning,
        Error
    }

    public abstract class GroupAttribute : Attribute
    {
        public string GroupName { get; }

        protected GroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class FoldoutGroupAttribute : GroupAttribute
    {
        public bool Expanded { get; }
        public bool HasDefinedExpanded { get; }

        public FoldoutGroupAttribute(string groupName) : base(groupName)
        {
        }

        public FoldoutGroupAttribute(string groupName, bool expanded) : base(groupName)
        {
            Expanded = expanded;
            HasDefinedExpanded = true;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class BoxGroupAttribute : GroupAttribute
    {
        public BoxGroupAttribute(string groupName) : base(groupName)
        {
        }
    }

    public abstract class ConditionAttribute : Attribute
    {
        public string MemberName { get; }

        protected ConditionAttribute(string memberName)
        {
            MemberName = memberName;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class ShowIfAttribute : ConditionAttribute
    {
        public ShowIfAttribute(string memberName) : base(memberName)
        {
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class HideIfAttribute : ConditionAttribute
    {
        public HideIfAttribute(string memberName) : base(memberName)
        {
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class EnableIfAttribute : ConditionAttribute
    {
        public EnableIfAttribute(string memberName) : base(memberName)
        {
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class DisableIfAttribute : ConditionAttribute
    {
        public DisableIfAttribute(string memberName) : base(memberName)
        {
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class InfoBoxAttribute : Attribute
    {
        public string Message { get; }
        public InfoMessageType MessageType { get; }
        public string VisibleIf { get; }

        public InfoBoxAttribute(string message, InfoMessageType messageType = InfoMessageType.Info, string visibleIf = null)
        {
            Message = message;
            MessageType = messageType;
            VisibleIf = visibleIf;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Name { get; }

        public ButtonAttribute(string name = null)
        {
            Name = name;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonGroupAttribute : Attribute
    {
        public string GroupName { get; }

        public ButtonGroupAttribute(string groupName = "_DefaultGroup")
        {
            GroupName = groupName;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Property)]
    public sealed class PropertyOrderAttribute : Attribute
    {
        public int Order { get; }

        public PropertyOrderAttribute(int order)
        {
            Order = order;
        }
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class ReadOnlyAttribute : Attribute
    {
    }

    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InlineEditorAttribute : Attribute
    {
    }
}
