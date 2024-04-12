using System.Threading;
using System.Threading.Tasks;

namespace IRAAS.Middleware
{
    internal class SemaphoreFacade
    {
        private readonly SemaphoreSlim _semaphore;

        internal SemaphoreFacade(int count)
        {
            if (count > 0)
            {
                _semaphore = new SemaphoreSlim(count);
            }
        }

        public async Task<bool> WaitAsync(int ms)
        {
            return _semaphore is null || 
                await _semaphore.WaitAsync(ms);
        }

        public void Release()
        {
            _semaphore?.Release();
        }
    }
}