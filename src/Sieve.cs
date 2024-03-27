namespace Caching;

/// <summary>
/// Implements a SIEVE cache eviction policy as described in
/// <see cref="https://cachemon.github.io/SIEVE-website/blog/2023/12/17/sieve-is-simpler-than-lru/">SIEVE</see>
/// </summary>
/// <typeparam name="TKey">Type of the cache key</typeparam>
public class Sieve<TKey> {
    readonly LinkedList<Entry> expiration = new();
    readonly Dictionary<TKey, LinkedListNode<Entry>> entries;
    LinkedListNode<Entry>? hand;

    public Sieve(int capacity, IEqualityComparer<TKey>? comparer = null) {
        if (capacity <= 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

        long capacityConsideringLoadFactor = (capacity * 4L) / 3;
        if (capacityConsideringLoadFactor > int.MaxValue) {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity is too large.");
        }
        this.Capacity = capacity;
        this.entries = new Dictionary<TKey, LinkedListNode<Entry>>(capacity: (int)capacityConsideringLoadFactor, comparer);
    }

    /// <summary>
    /// Notifies the policy that the key was accessed. Returns <c>true</c> if an eviction happened.
    /// </summary>
    public bool Access(TKey key, out TKey? evicted) {
        bool evictionHappened = false;
        evicted = default;
        if (this.entries.TryGetValue(key, out var node)) {
            node.Value = node.Value with { visited = true };
            // TODO return node
        } else {
            if (this.entries.Count == this.Capacity) {
                evicted = this.Evict();
                evictionHappened = true;
            }
            var entry = new Entry { key = key, visited = false };
            var newNode = this.expiration.AddFirst(entry);
            this.entries[key] = newNode;
        }
        return evictionHappened;
    }

    TKey Evict() {
        var obj = this.hand ?? this.expiration.Last;
        while (obj is { Value.visited: true }) {
            obj.Value = obj.Value with { visited = false };
            obj = obj.Previous ?? this.expiration.Last;
        }
        this.hand = obj.Previous;
        this.entries.Remove(obj.Value.key);
        this.expiration.Remove(obj);
        return obj.Value.key;
    }

    public int Count => this.entries.Count;
    public int Capacity { get; }

    struct Entry {
        internal bool visited;
        internal TKey key;
    }
}
