namespace Caching;

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
}
