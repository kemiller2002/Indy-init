namespace Smoke.Tests

open Xunit
open Smoke

module SMK001Tests =
    /// (obligation test absent): appending 3 then 4 to an empty ledger yields a total of 7.
    [<Fact>]
    let ``ledger totals entries`` () : unit =
        let ledger = Ledger.empty |> Ledger.append 3 |> Ledger.append 4
        Assert.Equal(7, Ledger.total ledger)
