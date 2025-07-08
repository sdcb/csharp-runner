namespace Sdcb.CSharpRunner.Host;

#region public contracts
public interface IHaveMaxRuns
{
    int MaxRuns { get; }          // 必须 >0
    int CurrentRuns { get; set; } // 已运行次数
}

public sealed class RunLease<T> : IDisposable, IAsyncDisposable where T : IHaveMaxRuns
{
    internal RunLease(RoundRobinPool<T> pool, T value)
    {
        _pool = pool;
        Value = value;
    }

    public T Value { get; }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _pool.Return(Value);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return default;
    }

    int _disposed;
    readonly RoundRobinPool<T> _pool;
}
#endregion

public sealed class RoundRobinPool<T> where T : IHaveMaxRuns
{
    #region ctor / public surface
    public RoundRobinPool() : this([]) { }

    public RoundRobinPool(IEnumerable<T> initial)
    {
        foreach (T item in initial ?? [])
            Add(item);
    }

    public int Count
    {
        get
        {
            lock (_lock) return _alive.Count;
        }
    }

    public void Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (item.MaxRuns < 0) throw new ArgumentOutOfRangeException(nameof(item));
        lock (_lock)
        {
            if (_alive.Add(item))
            {
                _queue.Enqueue(item);
                _sema.Release();
            }
        }
    }

    public bool Remove(T item)
    {
        if (item == null) return false;
        lock (_lock)
        {
            if (!_alive.Contains(item))
                return false;                             // 已不在池中

            // 如果还在队列里——直接移除
            if (_queue.RemoveFirst(item))
            {
                _alive.Remove(item);
                _sema.Wait(0);                          // 抵消之前的 Release
                return true;
            }

            // 正在被租出：标记，待归还时再真正移除
            _toBeDropped.Add(item);
            return true;
        }
    }

    public bool Contains(T item)
    {
        lock (_lock) return _alive.Contains(item);
    }

    public RunLease<T> AcquireLease()
    {
        if (!_sema.Wait(0))
            throw new InvalidOperationException("Pool is empty.");

        return DequeueForLease();
    }

    public RunLease<T> WaitAcquireLease(CancellationToken ct = default)
    {
        _sema.Wait(ct);
        return DequeueForLease();
    }

    public async Task<RunLease<T>> AcquireLeaseAsync(CancellationToken ct = default)
    {
        await _sema.WaitAsync(ct).ConfigureAwait(false);
        return DequeueForLease();
    }
    #endregion

    #region private helpers
    // 仅由 RunLease 调用
    internal void Return(T item)
    {
        lock (_lock)
        {
            checked { item.CurrentRuns++; }

            bool exhausted = item.MaxRuns != 0 && item.CurrentRuns >= item.MaxRuns;
            bool manuallyRemoved = _toBeDropped.Remove(item);

            if (exhausted || manuallyRemoved)
            {
                _alive.Remove(item);
                return;                              // 不再可用，不要 Release()
            }

            _queue.Enqueue(item);
            _sema.Release();
        }
    }

    RunLease<T> DequeueForLease()
    {
        lock (_lock)
        {
            // SemaphoreSlim 保证此时队列里一定有元素
            T item = _queue.Dequeue();
            return new RunLease<T>(this, item);
        }
    }
    #endregion

    #region fields
    readonly object _lock = new();
    readonly Queue<T> _queue = new();
    readonly HashSet<T> _alive = [];
    readonly HashSet<T> _toBeDropped = [];
    readonly SemaphoreSlim _sema = new(0, int.MaxValue);
    #endregion
}

#region tiny helpers
static class QueueExtensions
{
    // 移除首个等于 item 的元素；O(n)
    public static bool RemoveFirst<T>(this Queue<T> q, T item)
    {
        if (q.Count == 0) return false;
        bool found = false;
        int original = q.Count;

        for (int i = 0; i < original; i++)
        {
            T? dequeued = q.Dequeue();
            if (!found && EqualityComparer<T>.Default.Equals(dequeued, item))
            {
                found = true;
                continue;          // 丢弃
            }
            q.Enqueue(dequeued);
        }
        return found;
    }
}
#endregion
