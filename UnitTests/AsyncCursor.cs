using MongoDB.Driver;

namespace UnitTests
{
    public class AsyncCursor<T> : IAsyncCursor<T>
    {
        private readonly IEnumerable<T> _items;
        private bool _moved;

        public AsyncCursor(IEnumerable<T> items)
        {
            _items = items;
            _moved = false;
        }

        public IEnumerable<T> Current => _moved ? _items : Enumerable.Empty<T>();

        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            if (!_moved)
            {
                _moved = true;
                return _items.Any();
            }
            return false;
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MoveNext(cancellationToken));
        }

        public void Dispose() { }
    }
}
