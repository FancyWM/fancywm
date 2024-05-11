using System;
using System.Collections.Generic;
using System.Diagnostics;

using WinMan;

namespace FancyWM.Layouts.Tiling
{
    public class DesktopTree
    {
        public PanelNode? Root
        {
            get => m_root;
            set
            {
                if (m_root != null)
                {
                    m_root.Desktop = null;
                }
                m_wnd2Node = [];

                m_root = value;
                if (m_root != null)
                {
                    m_root.Desktop = this;

                    foreach (var window in m_root.Windows)
                    {
                        m_wnd2Node.Add(window.WindowReference, window);
                    }
                }
            }
        }

        public Rectangle WorkArea { get; set; }

        private PanelNode? m_root;
        private Dictionary<IWindow, WindowNode> m_wnd2Node = [];

        public WindowNode? FindNode(IWindow window)
        {
            if (m_wnd2Node.TryGetValue(window, out WindowNode? node))
            {
                return node;
            }
            return null;
        }

        public void Arrange()
        {
            if (Root == null)
            {
                throw new InvalidOperationException($"{nameof(Root)} is null!");
            }
            Root.Arrange(new RectangleF(WorkArea));
        }

        public void Measure()
        {
            if (Root == null)
            {
                throw new InvalidOperationException($"{nameof(Root)} is null!");
            }
            Root.Measure();
        }

        public static void SwapReferences(TilingNode nodeA, TilingNode nodeB)
        {
            var parentA = nodeA.Parent ?? throw new InvalidOperationException($"Parent of {nameof(nodeA)} is null!");
            var indexA = parentA.IndexOf(nodeA);

            var parentB = nodeB.Parent ?? throw new InvalidOperationException($"Parent of {nameof(nodeB)} is null!");
            var indexB = parentB.IndexOf(nodeB);

            Debug.Assert(parentA != nodeB);
            Debug.Assert(parentB != nodeA);

            parentA.SetReference(indexA, nodeB);
            nodeB.Parent = parentA;
            parentB.SetReference(indexB, nodeA);
            nodeA.Parent = parentB;
        }

        internal void Register(WindowNode window)
        {
            m_wnd2Node.Add(window.WindowReference, window);
        }

        internal void Unregister(WindowNode window)
        {
            if (!m_wnd2Node.Remove(window.WindowReference))
            {
                throw new InvalidOperationException();
            }
        }
    }

    //class StackPanel : Panel
    //{
    //    public override NodeType Type => NodeType.Stack;
    //}
}
