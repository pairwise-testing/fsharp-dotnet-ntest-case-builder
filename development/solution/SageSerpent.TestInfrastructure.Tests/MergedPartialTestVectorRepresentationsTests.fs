﻿namespace SageSerpent.TestInfrastructure.Tests

    open NUnit.Framework
    
    open SageSerpent.Infrastructure
    open SageSerpent.TestInfrastructure
    open System
    open System.Windows.Forms
    open System.Drawing
    open System.Collections.Generic
    open Microsoft.FSharp.Collections
    
    
    [<TestFixture>]
    type MergedPartialTestVectorRepresentationsTestFixture () =
        let randomBehaviourSeed = 23
        
        let reciprocalOfProbabilityOfTerminatingRandomlyGeneratedPartOfPartialTestVector = 10u
        
        let reciprocalOfProbabilityOfNotGeneratingAnyFurtherPartialTestVectors = 30u
        
        let maximumNumberOfTestVariables = 70u
        
        let maximumNumberOfTestVectors = 400u
        
        let maximumRandomWalkStep = 10u
        
        let maximumLevelDelta = 5u
        
        let overallTestRepeats = 100u
        
        let maximumNumberOfIndicesToAvoid = 4u
        
        let createNonOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff =
            let randomBehaviour =
                RandomBehaviour randomBehaviourSeed
            let createPartialTestVectors () =
                let createShuffledUniqueLevels () =
                    randomBehaviour.Shuffle (seq {1u .. maximumNumberOfTestVectors})
                let shuffleForEachTestVariableIndex =
                    Array.init (int32 maximumNumberOfTestVariables) (fun _ -> createShuffledUniqueLevels ())
                let numberOfTestVectors =
                    randomBehaviour.ChooseAnyNumberFromOneTo maximumNumberOfTestVectors
                let rec fillInPartialTestVectors incompletePartialTestVectors
                                                 completedPartialTestVectors =
                    match incompletePartialTestVectors with
                        [] ->
                            raise (InternalAssertionViolationException "Base case for recursion is for *one* incomplete vector.")
                      | head :: [] ->
                            let rec ensureVectorIsNonEmpty vectorBeingCompleted =
                                let testVariableIndex =
                                    randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan maximumNumberOfTestVariables
                                let shuffledLevels =
                                    shuffleForEachTestVariableIndex.[int32 testVariableIndex]
                                let numberOfCompletedPartialTestVectors =
                                    List.length completedPartialTestVectors
                                let levelForVectorBeingCompleted =
                                    shuffledLevels.[numberOfCompletedPartialTestVectors]
                                Map.add testVariableIndex levelForVectorBeingCompleted
                                                          (if randomBehaviour.HeadsItIs ()
                                                           then vectorBeingCompleted
                                                           else ensureVectorIsNonEmpty vectorBeingCompleted)
                            ensureVectorIsNonEmpty head
                            :: completedPartialTestVectors                               
                      | head :: tail ->
                            let forceDistinctionBetweenVectors vectorBeingExamined
                                                               (modifiedCopiesOfVectorsBeingExamined
                                                                , vectorBeingCompleted) =
                                let testVariableIndex =
                                    randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan maximumNumberOfTestVariables
                                let shuffledLevels =
                                    shuffleForEachTestVariableIndex.[int32 testVariableIndex]
                                let numberOfCompletedPartialTestVectors =
                                    List.length completedPartialTestVectors
                                let numberOfCopiesOfVectorsBeingExamined =
                                    List.length modifiedCopiesOfVectorsBeingExamined
                                let levelForVectorBeingExamined =
                                    shuffledLevels.[int32 numberOfTestVectors - (1 + numberOfCopiesOfVectorsBeingExamined)]
                                let vectorBeingExamined =
                                    Map.add testVariableIndex levelForVectorBeingExamined vectorBeingExamined
                                let levelForVectorBeingCompleted =
                                    shuffledLevels.[numberOfCompletedPartialTestVectors]
                                let vectorBeingCompleted =
                                    Map.add testVariableIndex levelForVectorBeingCompleted vectorBeingCompleted
                                vectorBeingExamined :: modifiedCopiesOfVectorsBeingExamined
                                , vectorBeingCompleted
                            let modifiedIncompletePartialTestVectors
                                , completedPartialTestVector =
                                List.foldBack forceDistinctionBetweenVectors tail ([], head)
                            fillInPartialTestVectors modifiedIncompletePartialTestVectors
                                                     (completedPartialTestVector :: completedPartialTestVectors)
                let partialTestVectors =
                    fillInPartialTestVectors (List.init (int32 numberOfTestVectors) (fun _ -> Map.empty))
                                             []
                randomBehaviour.Shuffle partialTestVectors
                |> List.ofArray               
            for _ in 1u .. overallTestRepeats do
                testHandoff (createPartialTestVectors ())
                
        let createOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff =
            let randomBehaviour =
                RandomBehaviour randomBehaviourSeed
            let createPartialTestVectors () =
                let rec createPartialTestVectors testVariableIndex =
                    if testVariableIndex = maximumNumberOfTestVariables
                       || randomBehaviour.ChooseAnyNumberFromOneTo reciprocalOfProbabilityOfNotGeneratingAnyFurtherPartialTestVectors = 1u
                    then []
                    else let rec chooseTestVariableIndicesAndTheirLevels recursionDepth =
                            let maximumRecursionDepth = 50u
                            if recursionDepth = maximumRecursionDepth
                               || randomBehaviour.ChooseAnyNumberFromOneTo reciprocalOfProbabilityOfTerminatingRandomlyGeneratedPartOfPartialTestVector = 1u
                            then []
                            else let lowerBoundInclusive =
                                    if testVariableIndex > maximumRandomWalkStep
                                    then testVariableIndex - maximumRandomWalkStep
                                    else 0u
                                 let upperBoundInclusive =
                                    testVariableIndex + maximumRandomWalkStep
                                 let chosenAbitraryTestVariableIndex =
                                    lowerBoundInclusive + randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan (upperBoundInclusive + 1u - lowerBoundInclusive)
                                 (chosenAbitraryTestVariableIndex
                                  , randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan maximumLevelDelta)
                                 :: chooseTestVariableIndicesAndTheirLevels (recursionDepth + 1u)
                         let partialTestVector =
                            chooseTestVariableIndicesAndTheirLevels 0u
                            |> Map.ofList
                         partialTestVector :: createPartialTestVectors (testVariableIndex + 1u)
                createPartialTestVectors 0u
            for _ in 1u .. overallTestRepeats do
                testHandoff (createPartialTestVectors ())
                
        let mergeOrAddPartialTestVectors partialTestVectors initialCollection =
            partialTestVectors
            |> List.fold (fun (mergedPartialTestVectors: MergedPartialTestVectorRepresentations<_>) partialTestVector ->
                            mergedPartialTestVectors.MergeOrAdd partialTestVector)
                         initialCollection
                              
        let dumpPartialTestVector partialTestVector =
            printf "[ "
            partialTestVector
            |> Map.iter (fun testVariableIndex testLevel ->
                            printf "(%A, %A) " testVariableIndex testLevel)
            printf "]\n"
            
        [<Test>]
        member this.TestAdditionOfUnmergeableVectorsPreservesIndividualVectors () =
            let testHandoff partialTestVectorsThatDoNotOverlap =
                let mergedPartialTestVectors =
                    mergeOrAddPartialTestVectors partialTestVectorsThatDoNotOverlap MergedPartialTestVectorRepresentations.initial
                let shouldBeTrue =
                    Set.ofList partialTestVectorsThatDoNotOverlap = Set.ofSeq mergedPartialTestVectors
                if not shouldBeTrue
                then let originals = Set.ofList partialTestVectorsThatDoNotOverlap
                     let merged = Set.ofSeq mergedPartialTestVectors
                     let common = Set.intersect originals merged
                     printf "Only in originals:-\n"
                     (originals - common) |> Set.iter dumpPartialTestVector  
                     printf "Only in merged:-\n"
                     (merged - common) |> Set.iter dumpPartialTestVector
                     printf "Common:-\n"
                     common |> Set.iter dumpPartialTestVector
                Assert.IsTrue shouldBeTrue
            createNonOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff
            
        [<Test>]
        member this.TestAdditionOfPartialsOfExistingVectors () =
            let testHandoff partialTestVectorsThatDoNotOverlap =
                let mergedPartialTestVectors =
                    mergeOrAddPartialTestVectors partialTestVectorsThatDoNotOverlap MergedPartialTestVectorRepresentations.initial
                let randomBehaviour = RandomBehaviour randomBehaviourSeed
                let mutantsOrCopiesOf partialTestVector =
                    let maximumRecursionDepth = 50u
                    let rec createMutantOrCopy recursionDepth =
                        let mutantOrCopy =
                            match randomBehaviour.ChooseAnyNumberFromOneTo 3u with
                                1u ->
                                    randomBehaviour.ChooseSeveralOf (Map.toSeq partialTestVector)
                                                                    (randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan (uint32 partialTestVector.Count))
                                 |> Map.ofSeq
                              | 2u ->
                                    Map.toSeq partialTestVector
                                    |> Seq.take (int32 (randomBehaviour.ChooseAnyNumberFromOneTo (uint32 partialTestVector.Count - 1u)))
                                    |> Map.ofSeq
                              | 3u ->
                                    partialTestVector
                              | _ ->
                                    raise (InternalAssertionViolationException "This case should never be matched.")
                        mutantOrCopy
                        :: if recursionDepth = maximumRecursionDepth
                              || randomBehaviour.HeadsItIs ()
                           then []
                           else createMutantOrCopy (recursionDepth + 1u)
                    createMutantOrCopy 0u
                let partialTestVectorsThatAddNoNewInformation =
                    partialTestVectorsThatDoNotOverlap
                    |> List.map mutantsOrCopiesOf
                    |> List.concat
                let remergedPartialTestVectors =
                    mergeOrAddPartialTestVectors partialTestVectorsThatAddNoNewInformation mergedPartialTestVectors
                let shouldBeTrue =
                    (Set.ofList partialTestVectorsThatDoNotOverlap).Count = (Set.ofSeq remergedPartialTestVectors).Count
                if not shouldBeTrue
                then let originals = Set.ofList partialTestVectorsThatDoNotOverlap
                     let remerged = Set.ofSeq remergedPartialTestVectors
                     let common = Set.intersect originals remerged
                     printf "Only in originals:-\n"
                     (originals - common) |> Set.iter dumpPartialTestVector  
                     printf "Only in remerged:-\n"
                     (remerged - common) |> Set.iter dumpPartialTestVector
                     printf "Common:-\n"
                     common |> Set.iter dumpPartialTestVector
                Assert.IsTrue shouldBeTrue
            createNonOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff
            
        [<Test>]
        member this.TestMergingOfVectorsInWithExistingPartialVectors () =
            let randomBehaviour = RandomBehaviour randomBehaviourSeed
            let testHandoff partialTestVectorsThatDoNotOverlap =
                let sortedIndicesToAvoid =
                    randomBehaviour.ChooseSeveralOf (List.init (int32 maximumNumberOfTestVariables) (fun count -> uint32 count))
                                                    (randomBehaviour.ChooseAnyNumberFromOneTo maximumNumberOfIndicesToAvoid)
                    |> List.ofArray
                    |> List.sort
                let remappedPartialTestVectors =
                    partialTestVectorsThatDoNotOverlap
                    |> List.map (Map.toList >> List.map (function index
                                                                  , level ->
                                                                    BargainBasement.MappingAvoidingIndices sortedIndicesToAvoid index
                                                                    , level) >> Map.ofList)
                let mergedPartialTestVectors =
                    mergeOrAddPartialTestVectors remappedPartialTestVectors MergedPartialTestVectorRepresentations.initial
                let possiblyAddLevelsForIndices indicesToAdd partialTestVector =
                    let chosenIndicesToAdd =
                        randomBehaviour.ChooseSeveralOf indicesToAdd
                                                        (randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan (uint32 (Seq.length indicesToAdd) + 1u))
                    seq {for index in chosenIndicesToAdd do
                            yield index, index + uint32 (randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan maximumLevelDelta)}
                    |> (let addIndexAndLevel partialTestVector
                                             (testVariableIndex
                                              , level)
                                             =
                            Map.add testVariableIndex level partialTestVector
                        Seq.fold addIndexAndLevel partialTestVector)
                let partialTestVectorsWhichMayHaveThePreviouslyAvoidedIndices =
                    remappedPartialTestVectors
                    |> List.map (possiblyAddLevelsForIndices sortedIndicesToAvoid)
                let remergedPartialTestVectors =
                    mergeOrAddPartialTestVectors partialTestVectorsWhichMayHaveThePreviouslyAvoidedIndices mergedPartialTestVectors
                let shouldBeTrue =
                    Set.ofList partialTestVectorsWhichMayHaveThePreviouslyAvoidedIndices = Set.ofSeq remergedPartialTestVectors
//                if not shouldBeTrue
//                then let originals = Set.ofList partialTestVectorsWhichMayHaveThePreviouslyAvoidedIndices
//                     let remerged = Set.ofSeq remergedPartialTestVectors
//                     let common = Set.intersect originals remerged
//                     printf "remappedPartialTestVectors:\n"
//                     Set.ofList remappedPartialTestVectors |> Set.iter dumpPartialTestVector
//                     printf "mergedPartialTestVectors:\n"
//                     Set.ofSeq mergedPartialTestVectors |> Set.iter dumpPartialTestVector
//                     printf "Only in originals:-\n"
//                     (originals - common) |> Set.iter dumpPartialTestVector  
//                     printf "Only in remerged:-\n"
//                     (remerged - common) |> Set.iter dumpPartialTestVector
//                     printf "Common:-\n"
//                     common |> Set.iter dumpPartialTestVector
//                     printf "Indices to avoid: %A\n" sortedIndicesToAvoid
                Assert.IsTrue shouldBeTrue
            createNonOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff
            
        [<Test>]
        member this.TestVectorsAreEitherAddedOrMerged () =
            let testHandoff partialTestVectors =
                let mergedPartialTestVectors =
                        mergeOrAddPartialTestVectors partialTestVectors MergedPartialTestVectorRepresentations.initial
                let numberOfMergedPartialTestVectors =
                    Seq.length mergedPartialTestVectors
                let shouldBeTrue = numberOfMergedPartialTestVectors <= partialTestVectors.Length
                Assert.IsTrue shouldBeTrue
                let mergedPartialTestVectors =
                    Set.ofSeq mergedPartialTestVectors
                let partialVectorsThatWereChanged =
                    partialTestVectors
                    |> List.filter (fun partialTestVector -> not (mergedPartialTestVectors.Contains partialTestVector))
                    |> Set.ofSeq
                let checkInclusionInAtLeastOneMergedPartialTestVector partialTestVector =
                    let firstIncludesSecond first second =
                        (first
                         |> Map.toList
                         |> Set.ofList).IsSupersetOf (second
                                                       |> Map.toList
                                                       |> Set.ofList)
                    let shouldBeTrue =
                        mergedPartialTestVectors
                        |> Set.exists (fun mergedPartialTestVector
                                            -> firstIncludesSecond mergedPartialTestVector partialTestVector)
                    Assert.IsTrue shouldBeTrue
                partialVectorsThatWereChanged
                |> Seq.iter checkInclusionInAtLeastOneMergedPartialTestVector
            createOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff
            
        [<Test>]
        member this.TestAdditionOfTrivialEmptyPartialTestVectorsLeavesCollectionUnchanged () =
            let testHandoff partialTestVectors =
                let mergedPartialTestVectors =
                        mergeOrAddPartialTestVectors partialTestVectors MergedPartialTestVectorRepresentations.initial
                let remergedPartialTestVectors =
                        mergeOrAddPartialTestVectors [Map.empty] mergedPartialTestVectors
                let shouldBeTrue =
                    Set.ofSeq remergedPartialTestVectors = Set.ofSeq mergedPartialTestVectors
                Assert.IsTrue shouldBeTrue
            createOverlappingPartialTestVectorsAndHandEachOffToTest testHandoff
                
        [<Test>]
        member this.TestInitialStateIsEmptyAndDoesNotContainATrivialEmptyPartialTestVector () =
            let initial =
                MergedPartialTestVectorRepresentations.initial
            let containedPartialTestVectors =
                initial
                |> List.ofSeq
            let shouldBeTrue =
                containedPartialTestVectors.Length = 0
            Assert.IsTrue shouldBeTrue
                