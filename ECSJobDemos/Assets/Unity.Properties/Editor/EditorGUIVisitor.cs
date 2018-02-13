using System.Collections.Generic;
using Unity.Properties;
using UnityEditor;
using System;

public class EditorGUIVisitor : IPropertyVisitor,
    IPropertyVisitor<bool>,
    IPropertyVisitor<int>,
    IPropertyVisitor<float>,
    IPropertyVisitor<double>,
    IPropertyVisitor<string>
{
    private readonly Stack<IListProperty> _currentListStack = new Stack<IListProperty>();
    private IListProperty CurrentList => (_currentListStack.Count > 0) ? _currentListStack.Peek() : null;

    public bool BeginList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
    {
        EditorGUILayout.LabelField(context.Property.Name);
        EditorGUI.indentLevel++;
        _currentListStack.Push(context.Property);
        return true;
    }

    public bool BeginSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
    {
        EditorGUILayout.LabelField(context.Property.Name);
        EditorGUI.indentLevel++;
        return true;
    }

    public void EndList<TContainer, TValue>(ref TContainer container, ListContext<TValue> context) where TContainer : IPropertyContainer
    {
        _currentListStack.Pop();
        EditorGUI.indentLevel--;
    }

    public void EndSubtree<TContainer, TValue>(ref TContainer container, SubtreeContext<TValue> context) where TContainer : IPropertyContainer
    {
        EditorGUI.indentLevel--;
    }

    public bool Visit<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context) where TContainer : IPropertyContainer
    {
        EditorGUILayout.PrefixLabel($"Type {typeof(TValue).FullName} is not supported by EditorGUIVisitor");
        return true;
    }

    public bool VisitEnum<TContainer, TValue>(ref TContainer container, VisitContext<TValue> context)
        where TContainer : IPropertyContainer
        where TValue : struct
    {
        throw new NotImplementedException();
    }

    private bool DrawUI<TContainer, TValue>(VisitContext<TValue> context, ref TContainer container, Func<string, TValue, TValue> drawer)
        where TContainer : IPropertyContainer
    {
        var list = CurrentList;
        if (list == null)
        {
            var p = (IProperty<TContainer, TValue>)context.Property;
            p.SetValue(ref container, drawer(p.Name, context.Value));
        }
        else
        {
            var p = (IListProperty<TContainer, TValue>)list;
            p.SetValueAtIndex(container, context.Index, drawer(p.Name, context.Value));
        }

        return true;
    }

    public bool Visit<TContainer>(ref TContainer container, VisitContext<bool> context) where TContainer : IPropertyContainer
    {
        return DrawUI(context, ref container, (n, v) => EditorGUILayout.Toggle(n, v));
    }

    public bool Visit<TContainer>(ref TContainer container, VisitContext<int> context) where TContainer : IPropertyContainer
    {
        return DrawUI(context, ref container, (n, v) => EditorGUILayout.IntField(n, v));
    }

    public bool Visit<TContainer>(ref TContainer container, VisitContext<float> context) where TContainer : IPropertyContainer
    {
        return DrawUI(context, ref container, (n, v) => EditorGUILayout.FloatField(n, v));
    }

    public bool Visit<TContainer>(ref TContainer container, VisitContext<double> context) where TContainer : IPropertyContainer
    {
        return DrawUI(context, ref container, (n, v) => EditorGUILayout.DoubleField(n, v));
    }

    public bool Visit<TContainer>(ref TContainer container, VisitContext<string> context) where TContainer : IPropertyContainer
    {
        return DrawUI(context, ref container, (n, v) => EditorGUILayout.TextField(n, v));
    }
}

