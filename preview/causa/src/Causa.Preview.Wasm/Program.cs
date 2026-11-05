using System.Runtime.InteropServices.JavaScript;

namespace CausaPreviewWasm;

/// <summary>Moves one serialized Limen message into the F# engine and its answer back. Nothing else.</summary>
public static partial class Exports
{
    [JSExport]
    public static string Dispatch(string messageJson) => Causa.Preview.Dispatch.handle(messageJson);

    public static void Main() { }
}
