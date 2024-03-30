using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace FancyWM.Layouts
{
    public class UnsatisfiableFlexConstraintsException : ApplicationException
    {
        public Flex? Container { get; init; }

        public UnsatisfiableFlexConstraintsException() : this("Layout constraints not satisfiable!")
        {
        }

        public UnsatisfiableFlexConstraintsException(string message) : base(message)
        {
        }

        public UnsatisfiableFlexConstraintsException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public struct FlexConstraints(double width, double minWidth, double maxWidth)
    {
        public double Width = width;
        public double MinWidth = minWidth;
        public double MaxWidth = maxWidth;

        public override readonly string ToString()
        {
            return $"FlexItem {{ {MinWidth} <- {Width} -> {MaxWidth} }}";
        }
    }

    public class Flex : IReadOnlyList<FlexConstraints>
    {
        public double ContainerWidth { get; private set; } = 1;

        public double UsedWidth => m_items.Select(x => x.Width).Sum();

        public double MinWidth => m_items.Select(x => x.MinWidth).Sum();

        public double MaxWidth => m_items.Select(x => x.MaxWidth).Sum();

        public double UnusedWidth => ContainerWidth - UsedWidth;

        public int Count => m_items.Count;

        public FlexConstraints this[int index] => m_items[index];

        private List<FlexConstraints> m_items;

        public Flex()
        {
            m_items = [];
        }

        public Flex(Flex constraints)
        {
            ContainerWidth = constraints.ContainerWidth;
            m_items = [.. constraints.m_items];
        }

        public void InsertItem(int index, double minWidth, double maxWidth)
        {
            Debug.Assert(minWidth.Gte(0));
            Debug.Assert(minWidth.Lte(maxWidth));
            Debug.Assert(double.IsFinite(maxWidth));

            InTransaction(() =>
            {
                if (minWidth.Gt(ContainerWidth - MinWidth))
                {
                    throw new UnsatisfiableFlexConstraintsException { Container = this };
                }

                double finalWidth = AllocateUnsafe(minWidth, maxWidth);
                m_items.Insert(index, new FlexConstraints { Width = finalWidth, MinWidth = minWidth, MaxWidth = maxWidth });
            });
        }

        public void RemoveItem(int index)
        {
            var item = m_items[index];
            m_items.Remove(item);
            // This should never throw!
            ApplyDeltasUnsafe(item.Width);
        }

        public void UpdateConstraints(int index, double minWidth, double maxWidth)
        {
            InTransaction(() => UpdateConstraintsUnsafe(index, minWidth, maxWidth));
        }

        private void UpdateConstraintsUnsafe(int index, double minWidth, double maxWidth)
        {
            var c = m_items[index];
            double closestWidth = Math.Clamp(c.Width, minWidth, maxWidth);
            c.MinWidth = minWidth;
            c.MaxWidth = maxWidth;
            m_items[index] = c;
            if (m_items[index].Width != closestWidth)
            {
                ResizeItemUnsafe(index, closestWidth, GrowDirection.Both, resizeUniformly: false);
            }
        }

        public void UpdateConstraints((double minWidth, double maxWidth)[] newConstraints)
        {
            Debug.Assert(newConstraints.Length == m_items.Count);

            InTransaction(() =>
            {
                for (int index = 0; index < m_items.Count; index++)
                {
                    var (minWidth, maxWidth) = newConstraints[index];
                    var c = m_items[index];
                    c.MinWidth = minWidth;
                    c.MaxWidth = maxWidth;
                    m_items[index] = c;

                    if (minWidth.Gt(ContainerWidth))
                    {
                        throw new UnsatisfiableFlexConstraintsException();
                    }
                }

                for (int index = 0; index < m_items.Count; index++)
                {
                    var c = m_items[index];
                    double closestWidth = Math.Clamp(c.Width, c.MinWidth, c.MaxWidth);
                    if (!m_items[index].Width.Eq(closestWidth))
                    {
                        ResizeItemUnsafe(index, closestWidth, GrowDirection.Both, resizeUniformly: false);
                    }
                }
            });
        }

        public void ResizeItem(int index, double width, GrowDirection direction = GrowDirection.Both, bool resizeUniformly = false)
        {
            InTransaction(() => ResizeItemUnsafe(index, width, direction, resizeUniformly));
        }

        private void ResizeItemUnsafe(int index, double width, GrowDirection direction, bool resizeUniformly)
        {

            var c = m_items[index];
            if (width.Lt(c.MinWidth) || width.Gt(c.MaxWidth))
            {
                throw new UnsatisfiableFlexConstraintsException();
            }
            var delta = width - c.Width;

            var mask = CreateResizeMask(index, direction, resizeUniformly);
            if (!mask.Cast<bool>().Any(x => x))
            {
                throw new UnsatisfiableFlexConstraintsException();
            }

            if (delta.Gt(0))
            {
                var allocationSize = UnusedWidth - delta;
                double leftoverDelta = ApplyDeltasUnsafe(allocationSize, mask);

                // Try to minimize the leftover using the set of items
                mask.SetAll(false);
                mask.Set(index, true);
                leftoverDelta = ApplyDeltasUnsafe(leftoverDelta, mask);

                if (Math.Abs(leftoverDelta).Gt(0))
                {
                    throw new UnsatisfiableFlexConstraintsException();
                }
            }
            else
            {
                // Shrink the item
                var allocationSize = UnusedWidth - delta;
                ApplyDeltasUnsafe(allocationSize, mask);
            }

            c.Width = width;
            m_items[index] = c;

            Debug.Assert(UnusedWidth.Gte(0));
        }

        public void SetContainerWidth(double newWidth)
        {
            Debug.Assert(newWidth.Gte(0));
            Debug.Assert(double.IsFinite(newWidth));

            if (newWidth.Lt(MinWidth))
            {
                throw new UnsatisfiableFlexConstraintsException { Container = this };
            }

            ResizeContainer(newWidth);
        }

        private BitArray CreateResizeMask(int index, GrowDirection direction, bool resizeUniformly)
        {
            var mask = new BitArray(m_items.Count);
            if (resizeUniformly)
            {
                mask.Set(index, true);
                if (direction == GrowDirection.TowardsEnd)
                {
                    for (int i = 0; i < index; i++)
                    {
                        mask.Set(i, true);
                    }
                }
                else if (direction == GrowDirection.TowardsStart)
                {
                    for (int i = index + 1; i < m_items.Count; i++)
                    {
                        mask.Set(i, true);
                    }
                }
            }
            else
            {
                if (direction == GrowDirection.TowardsEnd)
                {
                    if (index + 1 < m_items.Count)
                    {
                        mask.SetAll(true);
                        mask.Set(index + 1, false);
                    }
                }
                else if (direction == GrowDirection.TowardsStart)
                {
                    if (index > 0)
                    {
                        mask.SetAll(true);
                        mask.Set(index - 1, false);
                    }
                }
                else
                {
                    mask.SetAll(false);
                    mask.Set(index, true);
                }
            }
            return mask;
        }

        public void MoveItem(int fromIndex, int toIndex)
        {
            var item = m_items[fromIndex];
            m_items.RemoveAt(fromIndex);
            m_items.Insert(toIndex, item);
        }

        private double AllocateUnsafe(double minWidth, double maxWidth)
        {
            Debug.Assert(minWidth.Gte(0));
            Debug.Assert(minWidth.Lte(maxWidth));
            Debug.Assert(double.IsFinite(minWidth) && double.IsFinite(maxWidth));

            double optimalWidth = Math.Min(ContainerWidth - MinWidth, ContainerWidth / (m_items.Count + 1));
            double autoWidth = Math.Clamp(Math.Max(optimalWidth, UnusedWidth), minWidth, maxWidth);

            Debug.Assert(minWidth.Lte(autoWidth));

            if (autoWidth.Gt(UnusedWidth))
            {
                double unavailable = ApplyDeltasUnsafe(UnusedWidth - autoWidth);
                if (unavailable.Gt(0))
                {
                    throw new UnsatisfiableFlexConstraintsException { Container = this };
                }
            }

            return autoWidth;
        }

        private void ResizeContainer(double newWidth)
        {
            double scaleFactor = newWidth / ContainerWidth;
            m_items = m_items
                .Select(x => new FlexConstraints(Math.Clamp(x.Width * scaleFactor, x.MinWidth, x.MaxWidth), x.MinWidth, x.MaxWidth))
                .ToList();
            ContainerWidth = newWidth;
        }

        /// <summary>
        /// Reclaims the specified amount of width uniformly from all items.
        /// Returns the amount that couldn't be reclaimed (or 0).
        /// </summary>
        private double ApplyDeltasUnsafe(double totalDelta, BitArray? mask = null)
        {
            Debug.Assert(mask == null || mask.Count == m_items.Count);
            Debug.Assert(double.IsFinite(totalDelta));
            if (Math.Abs(totalDelta).Lte(0))
            {
                return 0;
            }

            (double value, double boundary)[] constraints = m_items
                .Select(x => totalDelta.Gte(0)
                    ? (value: x.Width, boundary: Math.Min(x.MaxWidth, ContainerWidth))
                    : (value: x.Width, boundary: x.MinWidth))
                // Normalize values to fractions of the container width
                .Select(x => (x.value / ContainerWidth, x.boundary / ContainerWidth))
                .ToArray();

            BitArray done = mask ?? new BitArray(m_items.Count);

            // Delta as a fraction of the container width
            double normalizedDelta = totalDelta / ContainerWidth;

            // How much available space we actually have as a fraction of the container width
            double normalizedAvailableSpace = constraints
                .Select((x, i) => done[i] ? 0 : x.boundary - x.value)
                .Sum();

            if (normalizedAvailableSpace.Eq(0))
                return -normalizedDelta;

            // How much we need to change the combined width of all elements
            double deltaFactor = Math.Min(1, normalizedDelta / normalizedAvailableSpace);

            for (int i = 0; i < constraints.Length; i++)
            {
                if (done[i])
                {
                    continue;
                }

                double maxDelta = constraints[i].boundary - constraints[i].value;
                constraints[i].value += deltaFactor * maxDelta;
            }

            m_items = constraints
                // Denormalize values
                .Select(x => (value: x.value * ContainerWidth, boundary: x.boundary * ContainerWidth))
                .Select((x, i) => new FlexConstraints
                {
                    Width = x.value,
                    MinWidth = totalDelta.Lte(0) ? x.boundary : m_items[i].MinWidth,
                    MaxWidth = totalDelta.Gte(0) ? x.boundary : m_items[i].MaxWidth,
                })
                .ToList();

            return Math.Max(0, normalizedAvailableSpace - normalizedDelta);
        }

        private void InTransaction(Action func)
        {
            var items = new List<FlexConstraints>(m_items);
            try
            {
                func();
            }
            catch (UnsatisfiableFlexConstraintsException)
            {
                m_items = items;
                throw;
            }
        }

        public IEnumerator<FlexConstraints> GetEnumerator() => m_items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
