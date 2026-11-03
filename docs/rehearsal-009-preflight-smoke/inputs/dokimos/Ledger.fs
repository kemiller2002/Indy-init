namespace Smoke

/// Synthetic requirement SMK-001: an append-only ledger whose total is the sum of its entries.
module Ledger =
    let empty : int list = []
    let append (amount: int) (ledger: int list) = ledger @ [ amount ]
    let total (ledger: int list) = List.sum ledger
