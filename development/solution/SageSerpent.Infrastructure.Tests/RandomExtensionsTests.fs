﻿namespace SageSerpent.Infrastructure.Tests

    open NUnit.Framework

    open SageSerpent.Infrastructure
    open System
    open RandomExtensions

    [<TestFixture>]
    type RandomExtensionsTestFixture() =
        let inclusiveUpToExclusiveRange inclusiveLimit exclusiveLimit =
            Seq.init (int32 (exclusiveLimit - inclusiveLimit)) (fun x -> inclusiveLimit + uint32 x)

        let commonTestStructureForTestingOfChoosingSeveralItems testOnSuperSetAndItemsChosenFromIt =
            let  random = Random 1

            for inclusiveLowerBound in 58u .. 98u do
                for numberOfConsecutiveItems in 1u .. 50u do
                    let superSet = inclusiveUpToExclusiveRange inclusiveLowerBound (inclusiveLowerBound + numberOfConsecutiveItems) |> Set.ofSeq
                    for subsetSize in 1u .. numberOfConsecutiveItems do
                        for _ in 1 .. 10 do
                            let chosenItems = random.ChooseSeveralOf(superSet, subsetSize)
                            testOnSuperSetAndItemsChosenFromIt superSet chosenItems subsetSize

        let pig maximumUpperBound =
            let random = Random 678
            let concreteRangeOfIntegers = inclusiveUpToExclusiveRange 0u maximumUpperBound

            for _ in 1 .. 10 do
                let chosenItems = random.ChooseSeveralOf(concreteRangeOfIntegers, maximumUpperBound)
                for chosenItem in chosenItems do
                    ()

        let commonTestStructureForTestingAlternatePickingFromSequences testOnSequences =
            let randomBehaviour =
                Random 232
            for numberOfSequences in 0 .. 50 do
                let maximumPossibleNumberOfItemsInASequence =
                    100
                let sequenceSizes =
                    List.init numberOfSequences
                              (fun _ ->
                                randomBehaviour.ChooseAnyNumberFromZeroToOneLessThan (uint32 maximumPossibleNumberOfItemsInASequence)
                                |> int32)
                let sequences =
                    sequenceSizes
                    |> List.mapi (fun sequenceIndex
                                      sequenceSize ->
                                        Seq.init sequenceSize
                                                 (fun itemIndex ->
                                                    sequenceIndex + numberOfSequences * itemIndex))
                testOnSequences sequences

        [<Test>]
        member this.TestCoverageOfIntegersUpToExclusiveUpperBound() =
            let random = Random 29

            let maximumUpperBound = 30u

            for upperBound in 0u .. maximumUpperBound do
                let concreteRangeOfIntegers = inclusiveUpToExclusiveRange 0u upperBound

                let chosenItems = random.ChooseSeveralOf(concreteRangeOfIntegers, upperBound)
                let expectedRange = inclusiveUpToExclusiveRange 0u upperBound
                let shouldBeTrue = (chosenItems |> Set.ofArray) = (expectedRange |> Set.ofSeq)
                Assert.IsTrue shouldBeTrue

        [<Test>]
        member this.TestUniquenessOfIntegersProduced() =
            let random = Random 678

            let maximumUpperBound = 30u

            for upperBound in 0u .. maximumUpperBound do
                let concreteRangeOfIntegers = inclusiveUpToExclusiveRange 0u upperBound

                let chosenItems = random.ChooseSeveralOf(concreteRangeOfIntegers, upperBound)
                let shouldBeTrue = upperBound = (chosenItems |> Set.ofArray |> Seq.length |> uint32)
                Assert.IsTrue shouldBeTrue
                let shouldBeTrue = upperBound = (chosenItems |> Array.length |> uint32)
                Assert.IsTrue shouldBeTrue

        [<Test>]
        member this.TestDistributionOfSuccessiveSequencesWithTheSameUpperBound() =
            let random = Random 1

            let maximumUpperBound = 30u

            for upperBound in 0u .. maximumUpperBound do
                let concreteRangeOfIntegers = inclusiveUpToExclusiveRange 0u upperBound

                let numberOfTrials = 100000

                let itemToCountAndSumOfPositionsMap = Array.create (int32 upperBound) (0, 0.0)

                for _ in 1 .. numberOfTrials do
                    for position, item in random.ChooseSeveralOf(concreteRangeOfIntegers, upperBound) |> Seq.mapi (fun position item -> position, item) do
                        let count, sumOfPositions = itemToCountAndSumOfPositionsMap.[int32 item]
                        itemToCountAndSumOfPositionsMap.[int32 item] <- 1 + count, (float position + sumOfPositions)

                let toleranceEpsilon = 1e-1

                let shouldBeTrue =
                    itemToCountAndSumOfPositionsMap
                    |> Seq.forall (fun (count, sumOfPositions)
                                    -> let difference = (sumOfPositions / (float count) - float (0u + upperBound - 1u) / 2.0)
                                       difference < toleranceEpsilon)

                Assert.IsTrue shouldBeTrue

        [<Test>]
        member this.TestThatAllItemsChosenBelongToTheSourceSequence() =
            commonTestStructureForTestingOfChoosingSeveralItems (fun superSet chosenItems _ ->
                                                                    let shouldBeTrue = (chosenItems |> Set.ofArray).IsSubsetOf superSet
                                                                    Assert.IsTrue shouldBeTrue)

        [<Test>]
        member this.TestThatTheNumberOfItemsRequestedIsHonouredIfPossible() =
            commonTestStructureForTestingOfChoosingSeveralItems (fun _ chosenItems subsetSize ->
                                                                    let shouldBeTrue = (chosenItems |> Array.length) = (subsetSize |> int32)
                                                                    Assert.IsTrue shouldBeTrue)

        [<Test>]
        member this.TestThatUniqueItemsInTheSourceSequenceAreNotDuplicated() =
            commonTestStructureForTestingOfChoosingSeveralItems (fun _ chosenItems _ ->
                                                                    let shouldBeTrue = (chosenItems |> Set.ofArray |> Seq.length) = (chosenItems |> Array.length)
                                                                    Assert.IsTrue shouldBeTrue)

        [<Test>]
        member this.TestThatChoosingItemsRepeatedlyEventuallyCoversAllPermutations() =
            let empiricallyDeterminedMultiplicationFactorToEnsureCoverage = double 70500 / (BargainBasement.Factorial 7u |> double)

            let random = Random 1

            for inclusiveLowerBound in 58u .. 98u do
                for numberOfConsecutiveItems in 1u .. 7u do
                    let superSet = inclusiveUpToExclusiveRange inclusiveLowerBound (inclusiveLowerBound + numberOfConsecutiveItems) |> Set.ofSeq
                    for subsetSize in 1u .. numberOfConsecutiveItems do
                        let expectedNumberOfPermutations = BargainBasement.NumberOfPermutations numberOfConsecutiveItems subsetSize
                        let oversampledOutputs =
                            seq {
                                for _ in 1 .. Math.Ceiling(empiricallyDeterminedMultiplicationFactorToEnsureCoverage * double expectedNumberOfPermutations) |> int32 do
                                    yield random.ChooseSeveralOf(superSet, subsetSize) |> List.ofArray
                                }
                        let shouldBeTrue = oversampledOutputs |> Set.ofSeq |> Seq.length = (expectedNumberOfPermutations |> int32)
                        Assert.IsTrue shouldBeTrue

        [<Test>]
        member this.TestPig0GetInTheTrough() =
            pig 64000u

        [<Test>]
        member this.TestPig1() =
            pig 1000u

        [<Test>]
        member this.TestPig2() =
            pig 2000u

        [<Test>]
        member this.TestPig3() =
            pig 4000u

        [<Test>]
        member this.TestPig4() =
            pig 8000u

        [<Test>]
        member this.TestPig5() =
            pig 16000u

        [<Test>]
        member this.TestPig6() =
            pig 32000u

        [<Test>]
        member this.TestPig7() =
            pig 64000u

        [<Test>]
        member this.TestPig8() =
            pig 50000u

        [<Test>]
        member this.TestThatPickingAlternatelyFromSequencesPreservesTheItemsInTheOriginalSequences () =
            let randomBehaviour =
                Random 89734873
            let testHandoff sequences =
                let alternatelyPickedSequence =
                    randomBehaviour.PickAlternatelyFrom sequences
                let setOfAllItemsPickedFrom =
                    sequences
                    |> List.map Set.ofSeq
                    |> Set.unionMany
                let setofAllItemsActuallyPicked =
                    alternatelyPickedSequence
                    |> Set.ofSeq
                let shouldBeTrue =
                    setOfAllItemsPickedFrom = setofAllItemsActuallyPicked
                Assert.IsTrue shouldBeTrue
                let shouldBeTrue =
                    (sequences
                     |> List.map Seq.length
                     |> List.fold (+)
                                  0)
                     = (alternatelyPickedSequence
                        |> Seq.length)
                Assert.IsTrue shouldBeTrue
            commonTestStructureForTestingAlternatePickingFromSequences testHandoff

        [<Test>]
        member this.TestThatPickingAlternatelyFromSequencesPreservesTheOrderOfItemsInTheOriginalSequences () =
            let randomBehaviour =
                Random 2317667
            let testHandoff sequences =
                let alternatelyPickedSequence =
                    randomBehaviour.PickAlternatelyFrom sequences
                let numberOfSequences =
                    sequences
                    |> List.length
                let disentangledPickedSubsequences =
                    sequences
                    |> List.mapi (fun sequenceIndex
                                      sequence ->
                                      sequence
                                      |> Seq.filter (fun item ->
                                                        item % numberOfSequences
                                                         = sequenceIndex))
                let shouldBeTrue =
                    List.zip sequences
                             disentangledPickedSubsequences
                    |> List.forall (fun (sequence
                                         , disentangledPickedSubsequence) ->
                                         0 = Seq.compareWith compare
                                                             sequence
                                                             disentangledPickedSubsequence)
                Assert.IsTrue shouldBeTrue
            commonTestStructureForTestingAlternatePickingFromSequences testHandoff

        [<Test>]
        member this.TestThatPickingAlternatelyFromSequencesChoosesRandomlyFromTheSequences () =
            let randomBehaviour =
                Random 2317667
            let testHandoff sequences =
                let alternatelyPickedSequence =
                    randomBehaviour.PickAlternatelyFrom sequences
                let numberOfSequences =
                    sequences
                    |> List.length
                let sequenceIndexToPositionSumAndCount
                    , pickedSequenceLength =
                    alternatelyPickedSequence
                    |> Seq.fold (fun (sequenceIndexToPositionSumAndCount
                                      , itemPosition)
                                     item ->
                                    let sequenceIndex =
                                        item % numberOfSequences
                                    sequenceIndexToPositionSumAndCount
                                    |> match Map.tryFind sequenceIndex
                                                         sequenceIndexToPositionSumAndCount with
                                        Some (positionSum
                                              , numberOfPositions) ->
                                            Map.add sequenceIndex
                                                    (itemPosition + positionSum
                                                     , 1 + numberOfPositions)
                                                
                                      | None ->
                                            Map.add sequenceIndex
                                                    (itemPosition
                                                     , 1)
                                    , 1 + itemPosition)
                                (Map.empty
                                 , 0)

                let minumumRequiredNumberOfPositions
                    = 50
                let toleranceEpsilon =
                    6e-1
                for item
                    , (positionSum
                       , numberOfPositions) in Map.toSeq sequenceIndexToPositionSumAndCount do
                    if minumumRequiredNumberOfPositions <= numberOfPositions
                    then
                        let meanPosition =
                            (double) positionSum / (double) numberOfPositions
                        printf "Item: %A, mean position: %A, picked sequence length: %A\n" item
                                                                                           meanPosition
                                                                                           pickedSequenceLength
                        let shouldBeTrue =
                            Math.Abs (2.0 * meanPosition - (double) pickedSequenceLength) < (double) pickedSequenceLength * toleranceEpsilon
                        Assert.IsTrue shouldBeTrue
            commonTestStructureForTestingAlternatePickingFromSequences testHandoff
