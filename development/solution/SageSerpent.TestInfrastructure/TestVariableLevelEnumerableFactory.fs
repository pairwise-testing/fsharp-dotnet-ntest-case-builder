module SageSerpent.TestInfrastructure.TestVariableLevelEnumerableFactory

    open System.Collections
    open System

    let Create levels =
        let weaklyTypedLevels =
            levels
            |> Seq.map (fun level -> level :> Object)
            |> Array.ofSeq
        let node =
            SageSerpent.TestInfrastructure.TestVariableNode weaklyTypedLevels
        TestCaseEnumerableFactory node

        