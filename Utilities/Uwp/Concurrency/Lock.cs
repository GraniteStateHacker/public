using System;
using System.Threading;

namespace GraniteStateHacker.Utilities.Uwp.Concurrency
{
    /// <summary>
    /// Use an instance of this class to guard a resource with enforced timeouts and safer syntax
    /// than ReaderWriterLockSlim provides on its own.
    /// 
    /// Unlike the lock ([object]) {} mechanism, this class provides an exclusive and non exclusive guard.
    /// To make it easier to use, this class wraps System.Threading.ReaderWriterLockSlim
    /// in an IDisposable, making it easier to ensure locks are cleared correctly.  Simply wrap your guarded coded with
    /// using (myLockInstance.NotExclusive()) {  //guarded code goes here } for scoped activities that
    /// can run concurrently with other NonExclusive scopes, and using (myLockInstance.Exclusive) { } for 
    /// code that must operate on the resource exclusively.
    /// 
    /// There's room for improvements here, like making the timout configurable in a config file, but otherwise
    /// it's a reasonable start for a utility.
    /// 
    /// Understand that while Lock is an IDisposable itself, it's only cleaning up the underlying 
    /// ReaderWriterLockSlim.  It's intended to be used to guard a specific resource for the lifetime of
    /// that resource, even if that resource's lifetime isn't scoped.
    /// 
    /// </summary>
    /// <example>
    /// var myLock = new Lock();
    /// 
    /// MyTableData GetData(string tablename)
    /// {
    ///     using (myLock.NotExclusive()) 
    ///     {
    ///         return db.ReadTable(tablename);
    ///     }
    /// }
    /// </example>  
    /// <example>
    /// void SetData(string tablename, MyTableData content)
    /// {
    ///     using(myLock.Exclusive(Timespan.FromSeconds(3)) // Allow three seconds to acquire lock
    ///     {
    ///         db.UpdateTable(tablename, content);
    ///     }
    /// }
    /// </example>
    /// <example>
    /// void ConditionallyUpdateData(string tablename, MyTableData content)
    /// {
    ///     using(myLock.UpgradeableLock())
    ///     {
    ///         var isWriteNeeded = db.ReadTable(tablename);
    ///         if(isWriteNeeded)
    ///         {
    ///             using(myLock.Exclusive())
    ///             {
    ///                 db.UpdateTable(tablename, content);
    ///             }   
    ///         }
    ///     }
    /// }
    /// </example>
    public class Lock : IDisposable
    {
        ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        TimeSpan defaultTimeout = TimeSpan.FromSeconds(2);  //allow two seconds to acquire lock before throwing exception.

        public IDisposable NotExclusive()
        {
            return NotExclusive(defaultTimeout);
        }

        public IDisposable NotExclusive(TimeSpan timeout)
        {
            if (!_lock.TryEnterReadLock(timeout))
                throw new SynchronizationLockException($"Failed to acquire non-exclusive lock within the given timeout.  ({timeout.TotalMilliseconds}ms).");
            return new LockInternal(_lock.ExitReadLock);
        }

        public IDisposable Exclusive()
        {
            return Exclusive(defaultTimeout);
        }

        public IDisposable Exclusive(TimeSpan timeout)
        {
            if (!_lock.TryEnterWriteLock(timeout))
                throw new SynchronizationLockException($"Failed to acquire exclusive lock within the given timeout.  ({timeout.TotalMilliseconds}ms).");
            return new LockInternal(_lock.ExitWriteLock);
        }

        public IDisposable UpgradeableToExclusive()
        {
            return UpgradeableToExclusive(defaultTimeout);
        }

        public IDisposable UpgradeableToExclusive(TimeSpan timeout)
        {
            if (!_lock.TryEnterUpgradeableReadLock(timeout))
                throw new SynchronizationLockException($"Failed acquire upgradeable non-exclusive lock within the given timeout.  ({timeout.TotalMilliseconds}ms).");
            return new LockInternal(_lock.ExitUpgradeableReadLock);
        }

        public void Dispose()
        {
            try
            {
                _lock.Dispose();
            }
            catch (Exception) { }
        }

        private class LockInternal : IDisposable
        {
            Action _lockRelease;

            public LockInternal(Action lockRelease)
            {
                _lockRelease = lockRelease;
            }

            public void Dispose()
            {
                _lockRelease();
            }
        }
    }
}
