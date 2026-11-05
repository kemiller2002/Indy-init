// Practice only (FENCE.md). Marshalling glue: relays Limen protocol JSON to the F# engine with the clock and a source
// of fresh identifiers. No application decision is made here.
using System;
using System.Runtime.InteropServices.JavaScript;

namespace Causa.Practice.Host;

public static partial class HostShim
{
    [JSExport]
    public static string Dispatch(string message) =>
        Causa.Practice.Protocol.dispatch(DateTimeOffset.UtcNow, Microsoft.FSharp.Core.FuncConvert.FromFunc(() => Guid.NewGuid()), message);

    public static void Main() { }
}
