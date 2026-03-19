using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BatteryTracker.Contracts.Services;
using BatteryTracker.Helpers;
using Windows.Storage;

namespace BatteryTracker.Services
{
    internal sealed class AppLocalSettingsStorageService : ISettingsStorageService
    {
        public IDictionary<string, object> GetSettingsStorage()
        {
            if (RuntimeHelper.IsMSIX)
            {
                return ApplicationData.Current.LocalSettings.Values;
            }

            string baseDir = Path.Combine(AppContext.BaseDirectory, "Config");
            string path = Path.Combine(baseDir, "settings.json");

            return new FileBackedSettings(path);
        }

        private sealed class FileBackedSettings : IDictionary<string, object>
        {
            private readonly string _path;
            private readonly Dictionary<string, object> _inner;

            public FileBackedSettings(string path)
            {
                _path = path;
                _inner = Load(path);
            }

            public object this[string key]
            {
                get => _inner[key];
                set
                {
                    _inner[key] = value;
                    Save();
                }
            }

            public ICollection<string> Keys => _inner.Keys;
            public ICollection<object> Values => _inner.Values;
            public int Count => _inner.Count;
            public bool IsReadOnly => false;

            public void Add(string key, object value)
            {
                _inner.Add(key, value);
                Save();
            }

            public bool ContainsKey(string key) => _inner.ContainsKey(key);

            public bool Remove(string key)
            {
                bool removed = _inner.Remove(key);
                if (removed)
                {
                    Save();
                }
                return removed;
            }

            public bool TryGetValue(string key, out object value) => _inner.TryGetValue(key, out value!);

            public void Add(KeyValuePair<string, object> item)
            {
                _inner.Add(item.Key, item.Value);
                Save();
            }

            public void Clear()
            {
                _inner.Clear();
                Save();
            }

            public bool Contains(KeyValuePair<string, object> item) =>
                ((ICollection<KeyValuePair<string, object>>)_inner).Contains(item);

            public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) =>
                ((ICollection<KeyValuePair<string, object>>)_inner).CopyTo(array, arrayIndex);

            public bool Remove(KeyValuePair<string, object> item)
            {
                bool removed = ((ICollection<KeyValuePair<string, object>>)_inner).Remove(item);
                if (removed)
                {
                    Save();
                }
                return removed;
            }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _inner.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();

            private static Dictionary<string, object> Load(string path)
            {
                if (!File.Exists(path))
                {
                    return new Dictionary<string, object>();
                }

                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (data == null)
                    {
                        return new Dictionary<string, object>();
                    }

                    var result = new Dictionary<string, object>(data.Count);
                    foreach (var kvp in data)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                    return result;
                }
                catch
                {
                    return new Dictionary<string, object>();
                }
            }

            private void Save()
            {
                string? dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = new Dictionary<string, string>(_inner.Count);
                foreach (var kvp in _inner)
                {
                    data[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                }
                string json = JsonSerializer.Serialize(data);
                File.WriteAllText(_path, json);
            }
        }
    }
}
