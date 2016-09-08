using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using DynamicData.Cache.Internal;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData
{
    /// <summary>
    /// An observable cache
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal sealed class ObservableCache<TObject, TKey> : IObservableCache<TObject, TKey>
    {
        #region Fields

        private readonly ISubject<IChangeSet<TObject, TKey>> _changes = new Subject<IChangeSet<TObject, TKey>>();
        private readonly Lazy<ISubject<int>> _countChanged = new Lazy<ISubject<int>>(() => new Subject<int>());
        private readonly IReaderWriter<TObject, TKey> _readerWriter;
        private readonly IDisposable _cleanUp;
        private readonly object _locker = new object();
        private readonly object _writeLock = new object();
        #endregion

        #region Construction
         
        public ObservableCache(IObservable<IChangeSet<TObject, TKey>> source)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>();

            var loader = source
                .Synchronize(_locker)
                .FinallySafe(_changes.OnCompleted)
                .Subscribe(changes => _readerWriter.Write(changes)
                                                   .Then(InvokeNext, _changes.OnError)
                );

            _cleanUp = Disposable.Create(() =>
            {
                loader.Dispose();
                _changes.OnCompleted();
                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnCompleted();
            });
        }

        public ObservableCache(Func<TObject, TKey> keySelector = null)
        {
            _readerWriter = new ReaderWriter<TObject, TKey>(keySelector);

            _cleanUp = Disposable.Create(() =>
            {
                _changes.OnCompleted();
                if (_countChanged.IsValueCreated)
                    _countChanged.Value.OnCompleted();
            });
        }

        #endregion

        #region Updating (for internal purposes only)

        internal void UpdateFromIntermediate(Action<IIntermediateUpdater<TObject, TKey>> updateAction, Action<Exception> errorHandler = null)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            lock (_writeLock)
            {
                _readerWriter.Write(updateAction)
                    .Then(InvokeNext, errorHandler);
            }

        }

        internal void UpdateFromSource(Action<ISourceUpdater<TObject, TKey>> updateAction, Action<Exception> errorHandler = null)
        {
            if (updateAction == null) throw new ArgumentNullException(nameof(updateAction));
            lock (_writeLock)
            {
                _readerWriter.Write(updateAction)
                    .Then(InvokeNext, errorHandler);
            }
        }

        private void InvokeNext(IChangeSet<TObject, TKey> changes)
        {
            if (changes.Count == 0) return;

            lock (_locker)
            {
                try
                {
                    _changes.OnNext(changes);

                    if (_countChanged.IsValueCreated)
                        _countChanged.Value.OnNext(_readerWriter.Count);
                }
                catch (Exception ex)
                {
                    _changes.OnError(ex);
                }
            }
        }

        #endregion

        #region Accessors

        public Optional<TObject> Lookup(TKey key)
        {
            return _readerWriter.Lookup(key);
        }

        public IEnumerable<TKey> Keys => _readerWriter.Keys;

        public IEnumerable<KeyValuePair<TKey, TObject>> KeyValues => _readerWriter.KeyValues;

        public IEnumerable<TObject> Items => _readerWriter.Items;

        public int Count => _readerWriter.Count;

        #endregion

        #region Observable

        public IObservable<int> CountChanged => _countChanged.Value.StartWith(_readerWriter.Count).DistinctUntilChanged();

        public IObservable<Change<TObject, TKey>> Watch(TKey key)
        {
            return Observable.Create<Change<TObject, TKey>>
                (
                    observer =>
                    {
                        lock (_locker)
                        {
                            var initial = _readerWriter.Lookup(key);
                            if (initial.HasValue)
                                observer.OnNext(new Change<TObject, TKey>(ChangeReason.Add, key, initial.Value));

                            return _changes.FinallySafe(observer.OnCompleted).Subscribe(changes =>
                            {
                                var matches = changes.Where(update => update.Key.Equals(key));
                                foreach (var match in matches)
                                {
                                    observer.OnNext(match);
                                }
                            });
                        }
                    });
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect()
        {
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        lock (_locker)
                        {
                            var initial = GetInitialUpdates();
                            if (initial.Count > 0) observer.OnNext(initial);

                            return _changes.FinallySafe(observer.OnCompleted)
                                           .SubscribeSafe(observer);
                        }
                    });
        }

        public IObservable<IChangeSet<TObject, TKey>> Connect(Func<TObject, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            return Observable.Create<IChangeSet<TObject, TKey>>
                (
                    observer =>
                    {
                        lock (_locker)
                        {
                            var updater = new FilteredUpdater<TObject, TKey>(new ChangeAwareCache<TObject, TKey>(), predicate);
                            var filtered = updater.Update(GetInitialUpdates(predicate));
                            if (filtered.Count != 0)
                                observer.OnNext(filtered);

                            return _changes
                                .FinallySafe(observer.OnCompleted)
                                .Select(updater.Update)
                                .NotEmpty()
                                .SubscribeSafe(observer);
                        }
                    });
        }

        internal IChangeSet<TObject, TKey> GetInitialUpdates(Func<TObject, bool> filter = null)
        {
            return _readerWriter.AsInitialUpdates(filter);
        }

        #endregion

        public void Dispose()
        {
            _cleanUp.Dispose();
        }
    }
}
