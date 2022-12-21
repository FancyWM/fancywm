using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace FancyWM.Layouts
{
    public class Partition<E> : IReadOnlyList<(double weight, E value)>, IEnumerable<(double weight, E value)>
    {
        private List<double> m_weights = new List<double>();
        private List<E> m_values = new List<E>();

        public Partition()
        {
        }

        public Partition(IEnumerable<(double weight, E value)> enumerable)
        {
            m_weights = enumerable.Select(x => x.weight).ToList();
            m_values = enumerable.Select(x => x.value).ToList();
        }

        public (double weight, E value) this[int index]
        {
            get => (m_weights[index], m_values[index]);
            set => (m_weights[index], m_values[index]) = value;
        }

        public int Count => m_values.Count;

        public IReadOnlyList<double> Weights => m_weights;

        public IReadOnlyList<E> Values => m_values;

        public void Add(E value) => Insert(m_values.Count, value);

        public void Clear() => m_values.Clear();

        public bool Contains(E value) => m_values.Contains(value);

        public IEnumerator<(double weight, E value)> GetEnumerator()
        {
            for (int i = 0; i < m_values.Count; i++)
            {
                yield return (m_weights[i], m_values[i]);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(E value) => m_values.IndexOf(value);

        public void Insert(int index, E value)
        {
            var weight = 1.0 / (m_values.Count + 1);
            Allocate(weight);
            m_weights.Insert(index, weight);
            m_values.Insert(index, value);
        }

        public bool Remove(E value)
        {
            int index = IndexOf(value);
            if (index == -1) return false;
            RemoveAt(index);
            return true;
        }

        public void RemoveAt(int index)
        {
            var weight = m_weights[index];
            m_values.RemoveAt(index);
            m_weights.RemoveAt(index);
            Distribute(weight);
        }

        public void Resize(int index, double weight, GrowDirection direction)
        {
            if (weight > 1.0 || weight < 0.0)
            {
                throw new ArgumentException(nameof(weight));
            }

            var value = m_values[index];

            if (Count != 1)
            {
                if (direction == GrowDirection.Both)
                {
                    RemoveAt(index);
                    Allocate(weight);
                    m_weights.Insert(index, weight);
                    m_values.Insert(index, value);
                    return;
                }

                double deltaWeight = weight - m_weights[index];
                //if (deltaWeight < 0)
                //{
                //    // Negative growth flips direction
                //    direction = direction == GrowDirection.TowardsStart
                //        ? GrowDirection.TowardsEnd
                //        : GrowDirection.TowardsStart;
                //}

                if (direction == GrowDirection.TowardsStart)
                {
                    if (index == 0)
                    {
                        // No preceeding elements, fall back to uniform
                        Resize(index, weight, GrowDirection.Both);
                        return;
                    }

                    double predWeight = m_weights.Take(index).Sum();

                    for (int i = 0; i < index; i++)
                    {
                        m_weights[i] -= deltaWeight * m_weights[i] / predWeight;
                    }
                    m_weights[index] = weight;
                }
                else if (direction == GrowDirection.TowardsEnd)
                {
                    if (index == m_weights.Count - 1)
                    {
                        // No succeeding elements, fall back to uniform
                        Resize(index, weight, GrowDirection.Both);
                        return;
                    }
                    double succWeight = m_weights.Skip(index + 1).Sum();

                    for (int i = index + 1; i < m_weights.Count; i++)
                    {
                        m_weights[i] -= deltaWeight * m_weights[i] / succWeight;
                    }
                    m_weights[index] = weight;
                }
            }
        }

        private void Distribute(double weight)
        {
            var weightPerItem = weight / Count;
            for (int i = 0; i < Count; i++)
            {
                m_weights[i] += weightPerItem;
            }
            Verify();
        }

        private void Allocate(double weight)
        {
            Verify();
            for (int i = 0; i < Count; i++)
            {
                var shrinkBy = m_weights[i] * weight;
                m_weights[i] -= shrinkBy;
            }
        }

        private void Verify()
        {
            if (m_weights.Count > 0)
            {
                var sum = m_weights.Sum();
                if (sum < 0.99999 || sum > 1.00001)
                {
                    throw new InvalidProgramException();
                }
            }
        }
    }


}
