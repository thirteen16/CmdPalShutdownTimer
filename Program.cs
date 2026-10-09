// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license; see THIRD-PARTY-NOTICES.md.
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer.CsWinRT;

namespace ShutdownTimerExtension;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "-RegisterProcessAsComServer") return;

        var server = new Shmuelie.WinRTServer.ComServer();
        using var disposed = new ManualResetEvent(false);
        var extension = new ShutdownTimerExtension(disposed);
        server.RegisterClass<ShutdownTimerExtension, IExtension>(() => extension);
        server.Start();
        disposed.WaitOne();
        server.Stop();
        server.UnsafeDispose();
    }
}
