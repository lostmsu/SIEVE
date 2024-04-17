namespace Caching;

using System.IO;
using System.Threading.Tasks;

public class SieveTests {
    [Fact]
    public void DoesNotOvergrow() {
        var sieve = new Sieve<int>(capacity: 10);

        int evicted = 0;
        for (int added = 1; added <= 20; added++) {
            evicted += sieve.Access(added, out _) ? 1 : 0;
            int expected = added <= 10 ? added : 10;
            Assert.Equal(expected, sieve.Count);
            Assert.Equal(expected, added - evicted);
        }
    }

    [Fact]
    public void SaveRoundtrip() {
        var sieve = new Sieve<int>(capacity: 10);

        for (int added = 1; added <= 20; added++) {
            sieve.Access(added, out _);
        }
        for(int accessed = 20; accessed >= 18; accessed--) {
            sieve.Access(accessed, out _);
        }

        using var stream = new MemoryStream();
        sieve.Save(stream, (key, s) => Sieve<int>.WriteI32(s, key));

        stream.Position = 0;

        var loaded = Sieve<int>.Load(stream, s => Sieve<int>.ReadI32(s));

        Assert.Equal(sieve.Count, loaded.Count);
        Assert.Equal(sieve.Capacity, loaded.Capacity);
        Assert.Equal(sieve.HandIndex, loaded.HandIndex);
        Assert.Equal(sieve.expiration, loaded.expiration);
        Assert.Equal(sieve.entries.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)),
            loaded.entries.OrderBy(kv => kv.Key).Select(kv => (kv.Key, kv.Value.Value)));
    }
}
