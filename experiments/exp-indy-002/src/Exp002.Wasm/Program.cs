using System.Runtime.InteropServices.JavaScript;

namespace Exp002Wasm;

/// <summary>Forwards one serialized Limen message to the F# boundary and returns its answer. No decisions here.</summary>
public static partial class Exports
{
    // COMPOSITION (integrated): the real Exp002.Engine.Engine.ports; Exp002.Ui.Stub.ports is no longer referenced here.
    [JSExport]
    public static string Dispatch(string messageJson) => Exp002.Ui.Dispatch.handleWith(Exp002.Engine.Engine.ports, messageJson);

    public static void Main() { }
}
