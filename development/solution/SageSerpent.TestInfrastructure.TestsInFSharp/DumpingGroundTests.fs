﻿#light

namespace SageSerpent.TestInfrastructure.Tests

    open NUnit.Framework
    
    open SageSerpent.Infrastructure
    open SageSerpent.TestInfrastructure
    open System
    open System.Windows.Forms
    open System.Drawing
    
    type private TestLevel =
            Untracked of UInt32
          | Tracked of UInt32 * UInt32
          
    type DistributionModeWrtInterleavingNode =
            BetweenSiblingSubtrees
          | WithinOnlyASingleSubtree
    
    [<TestFixture>]
    type DumpingGroundTestFixture () =
        let maximumNumberOfTestVariables = 20u
        let maximumNumberOfTestLevelsForATestVariable = 10u
        let maximumNumberOfSubtreeHeadsPerAncestorNode = 5u
        let maximumDepthOfSubtreeWithOneOrNoTrackedTestVariables = 1u
        let randomSeed = 23
        
        [<Test>]
        member this.TestThatARandomlyChosenNCombinationOfTestVariablesIsCoveredInTermsOfCrossProductOfLevels () =
            let chooseAnyNumberFromZeroToOneLessThan = int32 >> (Random randomSeed).Next >> uint32
            let chooseAnyNumberFromOneTo = chooseAnyNumberFromZeroToOneLessThan >> (+) 1u
            let headsItIs () = chooseAnyNumberFromZeroToOneLessThan 2u = 0u
            for i in [0 .. 5] do
                let numberOfTestVariables = chooseAnyNumberFromOneTo maximumNumberOfTestVariables
                let numberOfTrackedTestVariables = chooseAnyNumberFromOneTo numberOfTestVariables
                let trackedTestVariableToNumberOfLevelsMap =
                    Map.of_list (List.init (int32 numberOfTrackedTestVariables)
                                           (fun testVariable -> testVariable, chooseAnyNumberFromOneTo maximumNumberOfTestLevelsForATestVariable))
                let rec createTree distributionModeWrtInterleavingNode
                                   indexForLeftmostTrackedTestVariable
                                   numberOfTrackedTestVariables
                                   maximumDepthOfSubtreeWithOneOrNoTrackedTestVariables =
                    let thinkAboutTerminatingRecursion = numberOfTrackedTestVariables <= 1u
                    if headsItIs () && thinkAboutTerminatingRecursion
                       || maximumDepthOfSubtreeWithOneOrNoTrackedTestVariables = 0u
                    then if numberOfTrackedTestVariables = 1u
                         then TestVariableNode ([for level in [1u .. trackedTestVariableToNumberOfLevelsMap.[int32 indexForLeftmostTrackedTestVariable]] do
                                                    yield box (Tracked (indexForLeftmostTrackedTestVariable, level))])
                              , indexForLeftmostTrackedTestVariable + 1u   
                         else TestVariableNode ([for level in [1u .. chooseAnyNumberFromOneTo maximumNumberOfTestLevelsForATestVariable] do
                                                    yield box (Untracked level)])
                              , indexForLeftmostTrackedTestVariable
                    else let distributionMakerForSynthesizingNodes numberOfSubtrees numberOfTrackedTestVariables =
                            let distributionsOfNumberOfTrackedTestVariablesForEachSubtree =
                                CombinatoricUtilities.ChooseContributionsToMeetTotal
                                    (List.init (int32 numberOfSubtrees) (fun _ -> numberOfTrackedTestVariables)) numberOfTrackedTestVariables
                            let chosenDistribution =
                                chooseAnyNumberFromZeroToOneLessThan (uint32 distributionsOfNumberOfTrackedTestVariablesForEachSubtree.Length)
                            List.nth distributionsOfNumberOfTrackedTestVariablesForEachSubtree (int32 chosenDistribution)
                         let distributionMakerForInterleavedNodes numberOfSubtrees numberOfTrackedTestVariables =
                            match distributionModeWrtInterleavingNode with
                                BetweenSiblingSubtrees when numberOfSubtrees > 1u ->
                                    let rec incrementOneOfTheEntriesWithoutPuttingAllOfTheTrackedVariablesInOneSubtree distribution =
                                        match distribution with
                                            [] -> raise (InternalAssertionViolationException "Either given an empty distribution or one with too high a limit.")
                                          | head :: tail -> if head + 1u = numberOfTrackedTestVariables
                                                            then head :: incrementOneOfTheEntriesWithoutPuttingAllOfTheTrackedVariablesInOneSubtree tail
                                                            else head + 1u :: tail
                                    incrementOneOfTheEntriesWithoutPuttingAllOfTheTrackedVariablesInOneSubtree
                                        (distributionMakerForSynthesizingNodes numberOfSubtrees (numberOfTrackedTestVariables - 1u))
                              | BetweenSiblingSubtrees when numberOfSubtrees = 1u -> [numberOfTrackedTestVariables]
                              | _ -> let choice = chooseAnyNumberFromOneTo numberOfSubtrees
                                     [for counter in [1u .. numberOfSubtrees] do
                                        if counter = choice
                                        then yield numberOfTrackedTestVariables
                                        else yield 0u]
                         let generateNode nodeFactory distributionMaker =
                            let numberOfSubtrees =
                                chooseAnyNumberFromOneTo maximumNumberOfSubtreeHeadsPerAncestorNode
                            let gatherSubtree (previouslyGatheredSubtrees, indexForLeftmostTrackedTestVariable) numberOfTrackedVariables =
                                let subtree, maximumTrackingVariableIndexFromSubtree =
                                    createTree distributionModeWrtInterleavingNode
                                               indexForLeftmostTrackedTestVariable
                                               numberOfTrackedVariables
                                               (if thinkAboutTerminatingRecursion
                                                then maximumDepthOfSubtreeWithOneOrNoTrackedTestVariables - 1u
                                                else maximumDepthOfSubtreeWithOneOrNoTrackedTestVariables)
                                subtree :: previouslyGatheredSubtrees, maximumTrackingVariableIndexFromSubtree
                            let distributionOfNumberOfTrackedTestVariablesForEachSubtree =
                                distributionMaker numberOfSubtrees numberOfTrackedTestVariables
                            let subtrees, maximumTrackingVariableIndex =
                                distributionOfNumberOfTrackedTestVariablesForEachSubtree                      
                                |> List.fold_left gatherSubtree ([], indexForLeftmostTrackedTestVariable)
                            nodeFactory subtrees, maximumTrackingVariableIndex
                         if headsItIs ()
                         then generateNode (fun subtrees -> InterleavingNode subtrees) distributionMakerForInterleavedNodes
                         else generateNode (fun subtrees -> SynthesizingNode subtrees) distributionMakerForSynthesizingNodes                          
                let tree, _ = createTree WithinOnlyASingleSubtree
                                         0u
                                         numberOfTrackedTestVariables
                                         maximumDepthOfSubtreeWithOneOrNoTrackedTestVariables
                let dumpTree tree =
                    let form = new Form ()
                    form.AutoSizeMode <- AutoSizeMode.GrowOnly
                    form.AutoSize <- true
                    let treeView = new TreeView ()
                    form.Controls.Add treeView
                    treeView.Dock <- DockStyle.Fill
                    let treeGuiNode = TreeNode ()
                    treeView.Nodes.Add treeGuiNode |> ignore
                    let rec dumpNode node (treeGuiNode: TreeNode) =
                        match node with
                            TestVariableNode levels -> let subtreeGuiNode = TreeNode ("TreeNode\n" + any_to_string levels)
                                                       treeGuiNode.Nodes.Add subtreeGuiNode |> ignore
                          | InterleavingNode subtrees -> let subtreeGuiNode = TreeNode ("InterleavingNode\n")
                                                         treeGuiNode.Nodes.Add subtreeGuiNode |> ignore
                                                         for subtree in subtrees do
                                                            dumpNode subtree subtreeGuiNode
                          | SynthesizingNode subtrees -> let subtreeGuiNode = TreeNode ("SynthesizingNode\n")
                                                         treeGuiNode.Nodes.Add subtreeGuiNode |> ignore
                                                         for subtree in subtrees do
                                                            dumpNode subtree subtreeGuiNode
                    dumpNode tree treeGuiNode
                    treeView.ExpandAll ()
                    form.ShowDialog () |> ignore
                dumpTree tree
            ()
       
    