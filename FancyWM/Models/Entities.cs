using System;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace FancyWM.Models
{
    public interface IObservableEntity<T> : IObservable<T>
    {
        IObservable<T> Value { get; }

        Task SaveAsync(Func<T, T> update);
    }

    public interface IObservableFileEntity<T> : IObservableEntity<T>
    {
        [JsonIgnore]
        string FullPath { get; }
    }

    public abstract class ObservableFileEntityBase<T> : IObservableFileEntity<T>
    {
        [JsonIgnore]
        public string FullPath { get; }

        public IObservable<T> Value => m_value;

        private readonly IObservable<T> m_value;
        private readonly Subject<T> m_saves = new();
        private T? m_currentValue;
        private readonly SemaphoreSlim m_rwLock = new(1, 1);

        protected ObservableFileEntityBase(string fullPath, Func<T> defaultFactory)
        {
            FullPath = fullPath;

            async Task<T> readAsync()
            {
                await m_rwLock.WaitAsync();
                try
                {
                    using var stream = File.OpenRead(FullPath);
                    return await ReadAsync(stream);
                }
                catch (Exception e) when (e is FileNotFoundException || e is JsonException || e is FormatException)
                {
                    // Continue
                }
                finally
                {
                    m_rwLock.Release();
                }

                var defaultValue = defaultFactory();
                m_currentValue = defaultValue;
                await SaveAsync(_ => _, notify: false);
                return defaultValue;
            }

            var lastValue = Observable.FromAsync(readAsync)
                .Merge(m_saves)
                .DistinctUntilChanged()
                .Do(value => m_currentValue = value)
                .Replay(1);
            lastValue.Connect();

            m_value = lastValue;
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            return Value.Subscribe(observer);
        }

        public virtual async Task SaveAsync(Func<T, T> update)
        {
            await SaveAsync(update, true);
        }

        protected abstract Task<T> ReadAsync(Stream stream);
        protected abstract Task WriteAsync(Stream stream, T value);

        private async Task SaveAsync(Func<T, T> update, bool notify)
        {
            if (FullPath == null)
            {
                throw new InvalidOperationException("Entity not linked to a file.");
            }

            if (m_currentValue == null)
            {
                throw new InvalidOperationException("Model not initialized!");
            }

            var newValue = update(m_currentValue);
            if (Equals(newValue, m_currentValue))
            {
                return;
            }

            await m_rwLock.WaitAsync();
            try
            {
                using var stream = File.Open(FullPath, FileMode.Create);
                await WriteAsync(stream, newValue);
            }
            finally
            {
                m_rwLock.Release();
            }

            if (notify)
            {
                m_saves.OnNext(newValue);
            }
        }
    }

    public class ObservableJsonEntity<T>(string fullPath, Func<T> defaultFactory, JsonSerializerOptions? options = null) : ObservableFileEntityBase<T>(fullPath, defaultFactory)
    {
        public JsonSerializerOptions Options { get; } = options ?? new JsonSerializerOptions();

        protected override async Task<T> ReadAsync(Stream stream)
        {
            var result = await JsonSerializer.DeserializeAsync<T>(stream, Options) ?? throw new InvalidOperationException("Value is null!");
            return result;
        }

        protected override async Task WriteAsync(Stream stream, T value)
        {
            await JsonSerializer.SerializeAsync(stream, value, Options);
            await stream.FlushAsync();
        }
    }

}
