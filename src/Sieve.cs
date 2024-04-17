namespace Caching;

using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Implements a SIEVE cache eviction policy as described in
/// <see cref="https://cachemon.github.io/SIEVE-website/blog/2023/12/17/sieve-is-simpler-than-lru/">SIEVE</see>
/// </summary>
/// <typeparam name="TKey">Type of the cache key</typeparam>
public class Sieve<TKey> {
    internal readonly LinkedList<Entry> expiration = new();
    internal readonly Dictionary<TKey, LinkedListNode<Entry>> entries;
    internal LinkedListNode<Entry>? hand;

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

    public void Save(Stream stream, Action<TKey, Stream> keyWriter, CancellationToken cancel = default) {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (keyWriter is null) throw new ArgumentNullException(nameof(keyWriter));

        stream.WriteByte(0); // Version

        WriteI32(stream, this.Capacity);
        WriteI32(stream, this.expiration.Count);

        int handIndex = -1;
        int index = 0;
        foreach (var node in Enumerate(this.expiration)) {
            cancel.ThrowIfCancellationRequested();

            stream.WriteByte(node.Value.visited ? (byte)1 : (byte)0);
            keyWriter(node.Value.key, stream);

            if (handIndex < 0 && node == this.hand)
                handIndex = index;
            index++;
        }

        if (handIndex >= 0)
            WriteI32(stream, handIndex);
    }

    internal static void WriteI32(Stream stream, int value) {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Value must be non-negative.");

        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }

    public static Sieve<TKey> Load(Stream stream,
                                   Func<Stream, TKey> keyReader,
                                   IEqualityComparer<TKey>? comparer = null) {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (keyReader is null) throw new ArgumentNullException(nameof(keyReader));

        int version = stream.ReadByte();
        if (version != 0) throw new InvalidDataException("Invalid version");

        int capacity = ReadI32(stream);
        int count = ReadI32(stream);
        var sieve = new Sieve<TKey>(capacity, comparer);
        for (int i = 0; i < count; i++) {
            bool visited = ReadByte(stream) != 0;
            TKey key = keyReader(stream);
            var entry = new Entry { visited = visited, key = key };
            sieve.entries[key] = sieve.expiration.AddLast(entry);
        }

        if (count > 0) {
            int handIndex = Sieve<TKey>.ReadI32(stream);
            sieve.hand = Sieve<TKey>.Enumerate(sieve.expiration).ElementAt(handIndex);
        }

        return sieve;
    }

    internal int HandIndex => Enumerate(this.expiration).TakeWhile(node => node != this.hand).Count();

    static IEnumerable<LinkedListNode<Entry>> Enumerate(LinkedList<Entry> list) {
        var node = list.First;
        while (node is not null) {
            yield return node;
            node = node.Next;
        }
    }

    static byte ReadByte(Stream stream) {
        int b = stream.ReadByte();
        if (b < 0) throw new EndOfStreamException();
        return (byte)b;
    }

    internal static int ReadI32(Stream stream) {
        int result = ReadByte(stream);
        result |= ReadByte(stream) << 8;
        result |= ReadByte(stream) << 16;
        result |= ReadByte(stream) << 24;
        return result;
    }

    internal struct Entry {
        internal bool visited;
        internal TKey key;
    }
}
