using System;
using System.Collections.Generic;
using static HotChocolate.Language.Rewriters.LangRewritersResources;

namespace HotChocolate.Language.Rewriters;

/// <summary>
/// Represents the default implementation of <see cref="ISyntaxNavigator" />
/// </summary>
public class DefaultSyntaxNavigator : ISyntaxNavigator
{
    private readonly List<ISyntaxNode> _ancestors = new();

    /// <inheritdoc cref="ISyntaxNavigator.Push"/>
    public void Push(ISyntaxNode node) => _ancestors.Add(node);

    /// <inheritdoc cref="ISyntaxNavigator.Pop"/>
    public void Pop(out ISyntaxNode node)
    {
        if (_ancestors.Count == 0)
        {
            throw new InvalidOperationException(DefaultSyntaxNavigator_NoAncestors);
        }

        node = _ancestors[_ancestors.Count - 1];
        _ancestors.RemoveAt(_ancestors.Count - 1);
    }

    /// <inheritdoc cref="ISyntaxNavigator.GetAncestor{TNode}"/>
    public TNode? GetAncestor<TNode>()
        where TNode : ISyntaxNode
    {
        for (var i = _ancestors.Count - 1; i >= 0; i--)
        {
            if (_ancestors[i] is TNode typedNode)
            {
                return typedNode;
            }
        }

        return default;
    }

    /// <inheritdoc cref="ISyntaxNavigator.GetAncestors{TNode}"/>
    public IEnumerable<TNode> GetAncestors<TNode>()
        where TNode : ISyntaxNode
    {
        for (var i = _ancestors.Count - 1; i >= 0; i--)
        {
            if (_ancestors[i] is TNode typedNode)
            {
                yield return typedNode;
            }
        }
    }

    /// <inheritdoc cref="ISyntaxNavigator.GetParent"/>
    public ISyntaxNode? GetParent() => GetAncestor<ISyntaxNode>();
}