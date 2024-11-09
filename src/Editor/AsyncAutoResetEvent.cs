using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kub.Kolinizer
{
    /// <summary>
    /// Constructs an awaitable controlled by a signal.
    /// Todo: upgrade signal from a bool to int using Interlocked.Increment/Decrement. -chuck
    /// </summary>
    public sealed class AsyncAutoResetEvent
    {
        private readonly Task s_completed = Task.FromResult(true);
        private readonly Queue<TaskCompletionSource<bool>> _waits = new Queue<TaskCompletionSource<bool>>();
        private bool _signaled;

        public Task WaitAsync()
        {
            lock (_waits)
            {
                if (_signaled)
                {
                    _signaled = false;
                    return s_completed;
                }
                else
                {
                    var tcs = new TaskCompletionSource<bool>();
                    _waits.Enqueue(tcs);
                    return tcs.Task;
                }
            }
        }

        public void Set()
        {
            TaskCompletionSource<bool> toRelease = null;

            lock (_waits)
            {
                if (_waits.Count > 0)
                {
                    toRelease = _waits.Dequeue();
                }
                else if (!_signaled)
                {
                    _signaled = true;
                }
            }

            toRelease?.SetResult(true);
        }
    }
}
