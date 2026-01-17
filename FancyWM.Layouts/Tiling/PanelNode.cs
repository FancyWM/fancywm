using System;
using System.Collections.Generic;
using System.Linq;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public enum TilingDirection
    {
        Left, Up, Right, Down
    }

    public enum PanelOrientation
    {
        Horizontal,
        Vertical,
    }

    public abstract class PanelNode : TilingNode
    {
        /// <summary>
        /// Spacing applied around WindowNode nodes.
        /// </summary>
        public int Spacing { get; set; }

        public abstract IReadOnlyList<TilingNode> Children { get; }

        public override IEnumerable<WindowNode> Windows => Children.SelectMany(x => x.Windows);

        public override IEnumerable<TilingNode> Nodes => Children.SelectMany(x => x.Nodes).Prepend(this);

        public int IndexOf(TilingNode node)
        {
            return Children.TakeWhile(x => x != node).Count();
        }

        internal abstract void SetReference(int index, TilingNode node);

        internal TilingNode ReplaceReference(int index, TilingNode node)
        {
            if (node.Parent != null)
            {
                throw new ArgumentException("Expected a detached node but parent is not null!");
            }
            if (Desktop == null)
            {
                throw new InvalidOperationException($"Node is not attached to Desktop!");
            }

            var originalNode = Children[index];
            SetReference(index, node);
            node.Parent = this;

            originalNode.Parent = null;
            foreach (var windowNode in originalNode.Windows)
            {
                Desktop.Unregister(windowNode);
            }

            return originalNode;
        }

        public void Attach(TilingNode node)
        {
            Attach(Children.Count, node);
        }

        public void Attach(int index, TilingNode node)
        {
            foreach (var window in node.Windows)
            {
                if (Desktop == null)
                {
                    throw new InvalidOperationException($"Node is not attached to Desktop!");
                }
                Desktop.Register(window);
            }
            node.Parent = this;
            AttachCore(index, node);
        }

        public void Detach(TilingNode node)
        {
            foreach (var window in node.Windows)
            {
                if (Desktop == null)
                {
                    throw new InvalidOperationException($"Node is not attached to Desktop!");
                }
                Desktop.Unregister(window);
            }
            node.Parent = null;
            DetachCore(node);
        }

        protected abstract void AttachCore(int index, TilingNode node);
        protected abstract void DetachCore(TilingNode node);

        /// <summary>
        /// The maximum size that this child can have.
        /// </summary>
        public abstract Point GetMaxChildSize(TilingNode node);

        /// <summary>
        /// The maximum size that this node can have, if it becomes a child.
        /// </summary>
        public abstract Point GetMaxSizeForInsert(TilingNode node);

        public abstract void Move(int fromIndex, int toIndex);

        public void Cleanup(bool collapse = false)
        {
            RemovePlaceholders();
            RemoveIfEmpty();
            if (collapse)
            {
                CollapseIfSingle();
            }
        }

        public void RemoveIfEmpty()
        {
            if (Children.Count == 0 && Parent != null)
            {
                var parent = Parent;
                Remove();
                if (parent.Children.Count <= 1)
                {
                    parent.Cleanup();
                }
            }
        }

        public void CollapseIfSingle()
        {
            if (Children.Count == 1 && Parent != null)
            {
                int index = Parent!.IndexOf(this);
                Parent.SetReference(index, Children[0]);
                Children[0].Parent = Parent;
                Parent = null;
            }
        }

        public void RemovePlaceholders()
        {
            foreach (var child in Children.OfType<PlaceholderNode>().ToList())
            {
                Detach(child);
            }
        }
    }
}
