using System.Collections;
using System.Collections.Generic;

namespace KoreForge.Metrics;

public sealed class OperationTags : IReadOnlyDictionary<string, string>
{
    private readonly IReadOnlyDictionary<string, string> _values;

    public OperationTags() : this(new Dictionary<string, string>())
    {
    }

    public OperationTags(IDictionary<string, string> values)
    {
        _values = new Dictionary<string, string>(values);
    }

    public int Count => _values.Count;

    public IEnumerable<string> Keys => _values.Keys;

    public IEnumerable<string> Values => _values.Values;

    public string this[string key] => _values[key];

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
