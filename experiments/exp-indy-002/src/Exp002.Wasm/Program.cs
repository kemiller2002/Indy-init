using System.Runtime.InteropServices.JavaScript;

namespace Exp002Wasm;

/// <summary>Forwards one serialized Limen message to the F# boundary and returns its answer. No decisions here.</summary>
public static partial class Exports
{
    // COMPOSITION: integration swaps Exp002.Ui.Stub.ports for Exp002.Engine.Engine.ports on this line only.
    [JSExport]
    public static string Dispatch(string messageJson) => Exp002.Ui.Dispatch.handleWith(Exp002.Ui.Stub.ports, messageJson);

    public static void Main() { }
}
