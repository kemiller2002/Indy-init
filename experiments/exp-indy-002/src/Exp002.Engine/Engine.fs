/// The real engine wiring handed to the UI (App.fs) through the contract's EnginePorts seam.
module Exp002.Engine.Engine

open Exp002.Contracts

let ports: EnginePorts =
    { ValidateStart = Validation.validateStart
      Decide = Domain.decide
      Replay = Domain.replay
      Encode = Persistence.encode
      Decode = Persistence.decode
      SaveTransition = Persistence.saveTransition }
