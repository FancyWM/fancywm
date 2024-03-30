using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace FancyWM.ViewModels
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    sealed class DerivedPropertyAttribute(params string[] dependencies) : Attribute
    {
        public string[] Dependencies { get; } = dependencies;
    }

    class ViewModelInfo
    {
        private static readonly ConcurrentDictionary<Type, ViewModelInfo> s_instances = new();

        private static readonly List<PropertyInfo> s_emptyPropertyList = [];

        private readonly Dictionary<string, PropertyInfo> m_properties = [];

        private readonly Dictionary<PropertyInfo, List<PropertyInfo>> m_dependedBy = [];

        public static ViewModelInfo Get(Type type)
        {
            Debug.Assert(typeof(ViewModelBase).IsAssignableFrom(type));
            return s_instances.GetOrAdd(type, t => new(t));
        }

        private ViewModelInfo(Type type)
        {
            m_dependedBy = [];

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                m_properties.Add(prop.Name, prop);
            }

            var derivedProps = props
                .Where(prop => prop.GetCustomAttributes().OfType<DerivedPropertyAttribute>().Any());

            foreach (var prop in derivedProps)
            {
                var deps = prop.GetCustomAttributes().OfType<DerivedPropertyAttribute>()
                    .First()
                    .Dependencies
                    .Select(propName => type.GetProperty(propName)!);
                foreach (var dep in deps)
                {
                    if (m_dependedBy.ContainsKey(dep))
                    {
                        m_dependedBy[dep].Add(prop);
                    }
                    else
                    {
                        m_dependedBy[dep] = [dep];
                    }
                }
            }
        }

        public IEnumerable<PropertyInfo> GetDerivedProperties(PropertyInfo baseProperty)
        {
            return m_dependedBy[baseProperty];
        }

        public IEnumerable<PropertyInfo> GetDerivedProperties(string baseProperty)
        {
            var prop = m_properties[baseProperty];
            return m_dependedBy.GetValueOrDefault(prop, s_emptyPropertyList);
        }
    }

    public abstract class ViewModelBase : INotifyPropertyChanged, IDisposable
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public Dispatcher Dispatcher { get; }

        private readonly ViewModelInfo m_info;

        public ViewModelBase()
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            m_info = ViewModelInfo.Get(GetType());
        }

        protected virtual void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException(nameof(propertyName));
            }
            if (!Equals(field, value))
            {
                field = value;
                NotifyPropertyChanged(propertyName);
            }
        }

        protected virtual void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException(nameof(propertyName));
            }
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            foreach (var dep in m_info.GetDerivedProperties(propertyName))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(dep.Name));
            }
        }

        public virtual void Dispose()
        {
            PropertyChanged = null;
        }
    }
}
