namespace ProteomIQon.Tests

open Expecto

module RunTests =

    [<EntryPoint>]
    let main args =

        Tests.runTestsWithArgs defaultConfig args Tests.testSimpleTests |> ignore
        Tests.runTestsWithArgs defaultConfig args Tests.tableSortTest |> ignore
        0

