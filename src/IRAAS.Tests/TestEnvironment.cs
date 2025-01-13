using System;
using PeanutButter.SimpleHTTPServer;
using PeanutButter.Utils;

namespace IRAAS.Tests;

public static class TestEnvironment
{
    private static HttpServerFactory _httpServerFactory;

    public static void Setup()
    {
        _httpServerFactory = new HttpServerFactory();
    }

    public static IPoolItem<IHttpServer> BorrowHttpServer()
    {
        if (_httpServerFactory is null)
        {
            throw new Exception("TestEnvironment already destroyed or not yet initialised");
        }
        return _httpServerFactory.Borrow();
    }

    public static void Teardown()
    {
        _httpServerFactory?.Dispose();
        _httpServerFactory = null;
    }
}