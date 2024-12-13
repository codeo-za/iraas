using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.Utils;

[assembly: Parallelizable(ParallelScope.All | ParallelScope.Children)]

namespace IRAAS.Tests;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeTearDown]
    public void GlobalTeardown()
    {
        HttpServerPool.DisposeInstance();
    }
}

public class ReusableHttpServer : HttpServer
{
    private bool _inUse = true;
    private readonly object _lock = new object();

    public bool IsInUse
    {
        get
        {
            lock (_lock)
            {
                return _inUse;
            }
        }
    }

    public ReusableHttpServer TryBorrow()
    {
        lock (_lock)
        {
            if (_inUse)
            {
                return null;
            }

            _inUse = true;
            return this;
        }
    }

    public override void Dispose()
    {
        lock (_lock)
        {
            _inUse = false;
            Reset();
        }
    }

    public void TrulyDispose()
    {
        base.Dispose();
    }
}

public class HttpServerPool
{
    private static readonly List<ReusableHttpServer> _servers
        = new List<ReusableHttpServer>();

    public static HttpServer Borrow()
    {
        lock (_servers)
        {
            var result = _servers
                .Select(s => s.TryBorrow())
                .FirstOrDefault();
            if (result == null)
            {
                result = new ReusableHttpServer();
                _servers.Add(result);
            }
            return result;
        }
    }

    public static void DisposeInstance()
    {
        lock (_servers)
        {
            var toDispose = _servers.ToArray();
            toDispose.ForEach(d => d.TrulyDispose());
            _servers.Clear();
        }
    }
}