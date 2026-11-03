namespace Smoke.Tests

open Xunit
open Smoke

module SMK001Tests =
    /// SMK-001-O1: appending 3 then 4 to an empty ledger yields a total of 7.
    [<Fact(Skip = "R9 smoke: deliberately skipped")>]
    let ``SMK-001-O1`` () : unit =
        let ledger = Ledger.empty |> Ledger.append 3 |> Ledger.append 4
        Assert.Equal(7, Ledger.total ledger)
