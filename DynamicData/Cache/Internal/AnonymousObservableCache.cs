using System;
using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal
{
    internal sealed class AnonymousObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        private readonly IObservableCache<TObject, TKey> _cache;

        public AnonymousObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _cache = new ObservableCache<TObject, TKey>(source);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public AnonymousObservableCache(IObservableCache<TObject, TKey> cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            _cache = cache;
        }

        #region Delgated Members

        /// <summary>
        /// A count changed observable starting with the current count
        /// </summary>
        public IObservable<int> CountChanged => _cache.CountChanged;

        /// <summary>
        /// Watches updates from a single item using the specified key
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return _cache.Watch(key);
        }

        /// <summary>
        /// Returns a stream of cache updates preceeded with the initital cache state
        /// </summary>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return _cache.Connect();
        }

        /// <summary>
        /// Returns a filtered stream of cache updates preceeded with the initital filtered state
        /// </summary>
        /// <param name="predicate">The filter.</param>
        /// <returns></returns>
        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate)
        {
            return _cache.Connect(predicate);
        }

        /// <summary>
        /// Gets the keys
        /// </summary>
        public IEnumerable<TKey> Keys => _cache.Keys;

        /// <summary>
        /// Gets the Items
        /// </summary>
        public IEnumerable<TObject> Items => _cache.Items;

        /// <summary>
        /// The total count of cached items
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// Gets the key value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _cache.KeyValues;

        /// <summary>
        /// Lookup a single item using the specified key.
        /// </summary>
        /// <remarks>
        /// Fast indexed lookup
        /// </remarks>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public Optional<TObject> Lookup(TKey key)
        {
            return _cache.Lookup(key);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _cache.Dispose();
        }

        #endregion
    }
}
