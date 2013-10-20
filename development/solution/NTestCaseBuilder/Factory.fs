﻿namespace NTestCaseBuilder

    open System.Collections
    open System.Collections.Generic
    open System
    open System.Runtime.Serialization.Formatters.Binary
    open System.IO
    open System.Numerics
    open SageSerpent.Infrastructure
    open SageSerpent.Infrastructure.RandomExtensions
    open SageSerpent.Infrastructure.IEnumerableExtensions

    module FactoryDetail =
        let baseSize =
            bigint (int32 Byte.MaxValue) + 1I

        let serialize (fullTestVector: FullTestVector) =
            let binaryFormatter = BinaryFormatter ()
            use memoryStream = new MemoryStream ()
            binaryFormatter.Serialize (memoryStream, fullTestVector)
            let byteArray =
                memoryStream.GetBuffer ()
            let byteArrayRepresentedByBigInteger =
                byteArray
                |> Seq.fold (fun representationOfPreviousBytes
                                 byte ->
                                 bigint (int32 byte) + representationOfPreviousBytes * baseSize)
                            1I  // NOTE: start with one rather than the obvious choice of zero: this avoids losing leading zero-bytes which
                                // from the point of view of building a number to the base of 'baseSize' are redundant.
            byteArrayRepresentedByBigInteger

        let deserialize byteArrayRepresentedByBigInteger =
            let binaryFormatter = BinaryFormatter ()
            let byteArray =
                byteArrayRepresentedByBigInteger
                |> Seq.unfold (fun representationOfBytes ->
                                if 1I = representationOfBytes   // NOTE: see comment in implementation of 'serialize'.
                                then
                                    None
                                else
                                    Some (byte (int32 (representationOfBytes % baseSize))
                                          , representationOfBytes / baseSize))
                |> Array.ofSeq
                |> Array.rev
            use memoryStream = new MemoryStream (byteArray)
            binaryFormatter.Deserialize memoryStream
            |> unbox
            : FullTestVector

        let makeDescriptionOfReproductionString fullTestVector =
            String.Format ("Encoded text for reproduction of test case follows on next line as C# string:\n\"{0}\"",
                           (serialize fullTestVector).ToString())
    open FactoryDetail

    type TestCaseReproductionException (fullTestVector
                                        , innerException) =
        inherit Exception (makeDescriptionOfReproductionString fullTestVector
                           , innerException)

    /// <summary>A factory that can create an enumerable sequence of test cases used in turn to drive a parameterised
    /// unit test. Each test case is presented as a parameter to repeated calls to the parameterised unit test. The test
    /// cases are configured in terms of 'test variables', each of which can be set a particular 'level'; a given
    /// assignment of a level to each test variable then determines the composition of a test case.</summary>

    /// <seealso cref="TestVariable.Create">Construction method.</seealso>
    /// <seealso cref="Synthesis.Create">Construction method.</seealso>
    /// <seealso cref="Interleaving.Create">Construction method.</seealso>

    /// <remarks>Test case enumerable factories form a composite data structure, where a factory is the root
    /// of a tree of simpler factories, and can itself be part of a larger tree (or even part of several trees,
    /// as sharing is permitted). Ultimately a valid tree of test case enumerable factories will have leaf node
    /// factories based on sequences of test variable levels: each leaf node factory produces a trivial
    /// sequence of test cases that are just the levels of its own test variable.

    /// Factories that are internal nodes in the tree belong to two types. The first type is a synthesizing
    /// factory: it creates a sequence of synthesized test cases, where each synthesized test case is created
    /// from several simpler input test cases taken from across distinct sequences provided by corresponding
    /// subtrees of the synthesizing factory. The second type is an interleaving factory: it creates a sequence
    /// by mixing the sequences provided by the subtrees of the interleaving factory.

    /// The crucial idea is that for any factory at the head of some subtree, then given a test variable
    /// combination strength, a sequence of test cases can be created that satisfies the property that every
    /// combination of that strength of levels from distinct test variables represented by the leaves exists
    /// in at least one of the test cases in the sequence, provided the following conditions are met:-
    /// 1. The number of distinct test variables is at least the requested combination strength (obviously).
    /// 2. For any combination in question above, the test variables that contribute the levels being combined
    /// all do so via paths up to the head of the tree that fuse together at synthesizing factories and *not*
    /// at interleaving factories.

    /// So for example, creating a sequence of strength two for a tree composed solely of synthesizing factories
    /// for the internal nodes and test variable leaf nodes would guarantee all-pairs coverage for all of the
    /// test variables - any pair of levels taken from distinct test variables would be found in at least one
    /// test case somewhere in the sequence.</remarks>

    /// <remarks>1. Levels belonging to the same test variable are never combined together via any of the
    /// internal factory nodes.</remarks>
    /// <remarks>2. Sharing a single sequence of test variable levels between different leaf node factories
    /// in a tree (or sharing tree node factories to create a DAG) does not affect the strength guarantees:
    /// any sharing of levels or nodes is 'expanded' to create the same effect as an equivalent tree structure
    /// containing duplicated test variables and levels.</remarks>
    /// <remarks>3. If for a combination of levels from distinct test variables, the test variables that
    /// contribute the levels being combined all do so via paths up to the head of the tree that fuse together
    /// at interleaving factories, then that combination *will not* occur in any test case in any sequence
    /// generated by the head of the tree.</remarks>
    /// <remarks>4. If some test variables can only be combined in strengths less than the requested strength (due
    /// to interleaving between some of the test variables), then the sequence will contain combinations of these
    /// variables in the maximum possible strength.</remarks>
    /// <remarks>5. If there are insufficient test variables to make up the combination strength, or there are
    /// enough test variables but not enough that are combined by synthesizing factories, then the sequence
    /// will 'do its best' by creating combinations of up to the highest strength possible, falling short of the
    /// requested strength.</remarks>
    /// <remarks>6. A factory only makes guarantees as to the strength of combination of levels that contribute
    /// via synthesis to a final test case: so if for example a synthesizing factory causes 'collisions' to occur
    /// whereby several distinct combinations of simpler test cases all create the same output test case, then no
    /// attempt will be made to work around this behaviour and try alternative combinations that satisfy the
    /// strength requirements but create fewer collisions.</remarks>
    type IFactory =
        /// <summary>Creates an enumerable sequence of test cases during execution and repeatedly executes
        /// a parameterised unit test for each test case in the sequence.</summary>
        /// <remarks>If an exception is thrown during execution of the unit test for some test case in the sequence, then this is
        /// wrapped as an inner exception into an instance of TestCaseReproductionException. The test case reproduction exception
        /// also contains a description of a text token (denoted by a string literal) that can be cut and pasted into a call to
        /// the ExecuteParameterisedUnitTestForReproducedTestCase method to reproduce the failing test case without going over
        /// the other successful test cases.</remarks>
        /// <param name="maximumDesiredStrength">Maximum strength of test variable combinations that must be covered by the sequence.</param>
        /// <param name="parameterisedUnitTest">A call to this delegate runs a unit test over the test case passed in as the single parameter.</param>
        /// <seealso cref="ExecuteParameterisedUnitTestForReproducedTestCase"/>
        abstract ExecuteParameterisedUnitTestForAllTestCases: Int32 * Action<Object> -> Int32

        /// <summary>Executes a parameterised unit test for some specific test case described by a reproduction string.</summary>
        /// <remarks>The reproduction string is obtained by catching (or examining via a debugger) a test case reproduction exception
        /// obtained while running ExecuteParameterisedUnitTestForAllTestCases.</remarks>
        /// <param name="parameterisedUnitTest">A call to this delegate runs a unit test over the test case passed in as the single parameter.</param>
        /// <param name="reproductionString">Encoded text that reproduces the test case: this is obtained by inspection of a test case reproduction
        /// exception when running a failing test via ExecuteParameterisedUnitTestForAllTestCases.</param>
        abstract ExecuteParameterisedUnitTestForReproducedTestCase: Action<Object> * String -> Unit

        /// <summary>Creates an emumerable sequence of test cases that provides a guarantee of strength of
        /// combination of test variables across the entire sequence.</summary>
        /// <param name="maximumDesiredStrength">Maximum strength of test variable combinations that must be covered by the sequence.</param>
        /// <returns>A sequence of test cases typed as a non-generic IEnumerable.</returns>
        abstract CreateEnumerable: Int32 -> IEnumerable

        /// <value>The maximum strength of combination of test variables that the factory can make guarantees about in a call to CreateEnumerable.</value>
        abstract MaximumStrength: Int32

        /// <summary>Creates a new factory based on 'this' with the addition of a filter. Any existing filters will continue to be honoured as well;
        /// the operation adds the filter without replacing any previous ones. The internal implementation will call the filters to decide whether
        /// or not to accept a combination of levels for some set of test variables - if a filter call returns false, the combination considered by
        /// that call will be blocked.</summary>.
        /// <remarks>By default, a factory will not contain any filters to start with - this method is used to populate a factory.</remarks>
        /// <remarks>Filter calls that return false do not simply create 'holes' in the resulting set of test cases yielded by calls to CreateEnumerable;
        /// the factory will repack its test cases so as to honour its strength guarantee in an optimal fashion, albeit with the rejected combination
        /// of levels excluded.</remarks>
        /// <remarks>The implementation is at liberty to make successive calls at any point to a filter for the same combination of test levels,
        /// and may also consider supersets and / or subsets of that combination, as well as overlapping combinations - the filter implementation
        /// must guarantee consistency of logic for such calls, otherwise the resulting behaviour is undefined.</remarks>
        /// <remarks>If any of the filters blocks a combination, it will be excluded - the filters act in conjunction to pass, disjunction to exclude.</remarks>
        /// <remarks>The combinations considered by the filter do *not* have to be complete combinations that would describe a full test case, although full combinations will also be considered.</remarks>
        /// <remarks>The test variable indices used as keys in the dictionary passed to the filter are taken so that zero represents the test variable from the leftmost test variable
        /// factory that is a leaf node used to build up 'this'. This holds even if 'this' forms part of a larger tree structure leading to a higher-level factory.</remarks>
        /// <remarks>Filters are free to disregard the actual value of a test variable levels and simply use the paired index for it instead. The index corresponds
        /// to the position of the level in the sequence used to construct the test variable level factory for the level's test variable. This is useful, for example,
        /// for when two test variables share the same family of levels and the filter is used to impose a constraint that the levels from both test variables can never be
        /// equal - comparing the indices allows a general-purpose filter to be written that avoids worrying about whether equality is defined for the level type.</remarks>
        /// <remarks>If there is are any synthesizing nodes anywhere in the subtree covered by 'this' that perform permutations, the filter is taken to apply to
        /// the test variable indices *before* the effect of the permutation - furthermore, the filter will be unaware of the presence of the additional test variables
        /// that generate the permutations - the indices of the test variables passed to it will be the same as if no permutations were specified.</remarks>
        /// <remarks>There are never any entries in the dictionary passed to a filter that correspond to singleton test variables.</remarks>
        /// <remarks>The test variables referenced by the dictionary passed to a filter can never be from alternate subtrees in an interleave, i.e. they correspond to well-formed test vectors.</remarks>
        /// <param name="filter">Delegate accepting a dictionary that describes the combination of levels being considered - each key in the dictionary is a test
        /// variable index denoting one of the test variables involved; the associated value is a pair of an index denoting the actual test level for that test
        /// variable in the combination, together with the value of the test level itself. Must return true to signify that the combination of levels is permitted, false if the combination
        /// must be excluded.</param>
        /// <returns>A new factory that is a copy of 'this' but with the additional filter included.</returns>
        abstract WithFilter: LevelCombinationFilter -> IFactory

        abstract WithMaximumStrength: Int32 -> IFactory

        abstract WithZeroStrengthCost: unit -> IFactory

    /// <summary>This extends the API provided by IFactory to deal with test cases of a specific type given
    /// by the type parameter TestCase.</summary>
    /// <seealso cref="IFactory">The core API provided by the baseclass.</seealso>
    type ITypedFactory<'TestCase> =
        inherit IFactory

        abstract ExecuteParameterisedUnitTestForAllTestCases: Int32 * Action<'TestCase> -> Int32

        abstract ExecuteParameterisedUnitTestForReproducedTestCase: Action<'TestCase> * String -> unit

        abstract CreateEnumerable: Int32 -> IEnumerable<'TestCase>

        abstract WithFilter: LevelCombinationFilter -> ITypedFactory<'TestCase>

        abstract WithMaximumStrength: Int32 -> ITypedFactory<'TestCase>

        abstract WithZeroStrengthCost: unit -> ITypedFactory<'TestCase>

    type internal INodeWrapper =
        abstract Node: Node

    type TypedFactoryImplementation<'TestCase> (node: Node) =
        interface INodeWrapper with
            member this.Node = node

        interface IFactory with

            member this.CreateEnumerable maximumDesiredStrength =
                (this :> ITypedFactory<'TestCase>).CreateEnumerable maximumDesiredStrength
                :> IEnumerable

            member this.MaximumStrength =
                match (this :> INodeWrapper).Node.PruneTree with
                    Some prunedNode ->
                        prunedNode.MaximumStrengthOfTestVariableCombination
                  | None ->
                        0

            member this.WithFilter filter =
                (this :> ITypedFactory<'TestCase>).WithFilter filter
                :> IFactory

            member this.WithMaximumStrength maximumStrength =
                (this :> ITypedFactory<'TestCase>).WithMaximumStrength maximumStrength
                :> IFactory

            member this.WithZeroStrengthCost () =
                (this :> ITypedFactory<'TestCase>).WithZeroStrengthCost ()
                :> IFactory

            member this.ExecuteParameterisedUnitTestForAllTestCases (maximumDesiredStrength
                                                                     , parameterisedUnitTest: Action<Object>) =
                this.ExecuteParameterisedUnitTestForAllTypedTestCasesWorkaroundForDelegateNonCovariance (maximumDesiredStrength
                                                                                                         , parameterisedUnitTest.Invoke)

            member this.ExecuteParameterisedUnitTestForReproducedTestCase (parameterisedUnitTest: Action<Object>
                                                                           , reproductionString) =
                this.ExecuteParameterisedUnitTestForReproducedTypedTestCaseWorkaroundForDelegateNonCovariance (parameterisedUnitTest.Invoke
                                                                                                               , reproductionString)

        interface ITypedFactory<'TestCase> with
            member this.CreateEnumerable maximumDesiredStrength =
                this.CreateEnumerableOfTypedTestCaseAndItsFullTestVector maximumDesiredStrength
                |> Seq.map fst

            member this.ExecuteParameterisedUnitTestForAllTestCases (maximumDesiredStrength
                                                                     , parameterisedUnitTest: Action<'TestCase>) =
                this.ExecuteParameterisedUnitTestForAllTypedTestCasesWorkaroundForDelegateNonCovariance (maximumDesiredStrength
                                                                                                         , parameterisedUnitTest.Invoke)

            member this.ExecuteParameterisedUnitTestForReproducedTestCase (parameterisedUnitTest: Action<'TestCase>
                                                                           , reproductionString) =
                this.ExecuteParameterisedUnitTestForReproducedTypedTestCaseWorkaroundForDelegateNonCovariance (parameterisedUnitTest.Invoke
                                                                                                               , reproductionString)

            member this.WithFilter (filter: LevelCombinationFilter): ITypedFactory<'TestCase> =
                TypedFactoryImplementation<'TestCase> ((this :> INodeWrapper).Node.WithFilter filter)
                :> ITypedFactory<'TestCase>

            member this.WithMaximumStrength maximumStrength =
                TypedFactoryImplementation<'TestCase> ((this :> INodeWrapper).Node.WithMaximumStrength (Some maximumStrength))
                :> ITypedFactory<'TestCase>

            member this.WithZeroStrengthCost () =
                TypedFactoryImplementation<'TestCase> ((this :> INodeWrapper).Node.WithZeroCost ())
                :> ITypedFactory<'TestCase>

        member private this.ExecuteParameterisedUnitTestForAllTypedTestCasesWorkaroundForDelegateNonCovariance (maximumDesiredStrength
                                                                                                                , parameterisedUnitTest) =
            let mutable count = 0
            for testCase
                , fullTestVector in this.CreateEnumerableOfTypedTestCaseAndItsFullTestVector maximumDesiredStrength do
                try
                    parameterisedUnitTest testCase
                    count <- count + 1
                with
                    anyException ->
                        raise (TestCaseReproductionException (fullTestVector
                                                              , anyException))
            count

        member private this.ExecuteParameterisedUnitTestForReproducedTypedTestCaseWorkaroundForDelegateNonCovariance (parameterisedUnitTest
                                                                                                                      , reproductionString) =
            match (this :> INodeWrapper).Node.PruneTree with
                Some prunedNode ->
                    let fullTestVector =
                        BigInteger.Parse reproductionString
                        |> deserialize
                    let finalValueCreator =
                        prunedNode.FinalValueCreator ()
                    let testCase =
                        finalValueCreator fullTestVector: 'TestCase
                    parameterisedUnitTest testCase
              | None ->
                    ()

        member private this.CreateEnumerableOfTypedTestCaseAndItsFullTestVector maximumDesiredStrength =
            match (this :> INodeWrapper).Node.PruneTree with
                Some prunedNode ->
                    let associationFromStrengthToPartialTestVectorRepresentations
                        , associationFromTestVariableIndexToNumberOfItsLevels =
                        prunedNode.AssociationFromStrengthToPartialTestVectorRepresentations maximumDesiredStrength
                    let overallNumberOfTestVariables =
                        prunedNode.CountTestVariables
                    let randomBehaviour =
                        Random 0
                    let partialTestVectorsInOrderOfDecreasingStrength = // Order by decreasing strength so that high strength vectors get in there
                                                                        // first. Hopefully the lesser strength vectors should have a greater chance
                                                                        // of finding an earlier, larger vector to merge with this way.
                        (seq
                            {
                                for partialTestVectorsAtTheSameStrength in associationFromStrengthToPartialTestVectorRepresentations
                                                                            |> Seq.sortBy (fun keyValuePair -> - keyValuePair.Key)
                                                                            |> Seq.map (fun keyValuePair -> keyValuePair.Value) do
                                    yield! partialTestVectorsAtTheSameStrength
                            })

                    let lazilyProduceMergedPartialTestVectors mergedPartialTestVectorRepresentations
                                                              partialTestVectors =
                        // TODO: use 'Seq.unfold' here!
                        seq
                            {
                                let locallyModifiedMergedPartialTestVectorRepresentations =
                                    ref mergedPartialTestVectorRepresentations

                                for partialTestVector in partialTestVectors do
                                    match (!locallyModifiedMergedPartialTestVectorRepresentations: MergedPartialTestVectorRepresentations<_>).MergeOrAdd partialTestVector with
                                        updatedMergedPartialTestVectorRepresentationsWithFullTestCaseVectorSuppressed
                                        , Some resultingFullTestCaseVector ->
                                            yield resultingFullTestCaseVector

                                            locallyModifiedMergedPartialTestVectorRepresentations := updatedMergedPartialTestVectorRepresentationsWithFullTestCaseVectorSuppressed
                                      | updatedMergedPartialTestVectorRepresentations
                                        , None ->
                                            locallyModifiedMergedPartialTestVectorRepresentations := updatedMergedPartialTestVectorRepresentations

                                yield! (!locallyModifiedMergedPartialTestVectorRepresentations).EnumerationOfMergedTestVectors false
                            }

                    let lazilyProducedMergedPartialTestVectors =
                        lazilyProduceMergedPartialTestVectors (MergedPartialTestVectorRepresentations.Initial overallNumberOfTestVariables
                                                                                                              prunedNode.CombinedFilter)
                                                              partialTestVectorsInOrderOfDecreasingStrength

                    let finalValueCreator =
                        prunedNode.FinalValueCreator ()
                    seq
                        {
                            for mergedPartialTestVector in lazilyProducedMergedPartialTestVectors  do
                                match prunedNode.FillOutPartialTestVectorRepresentation mergedPartialTestVector
                                                                                        randomBehaviour with
                                    Some fullTestVector ->
                                        yield (finalValueCreator fullTestVector: 'TestCase)
                                              , fullTestVector
                                   | None ->
                                        ()
                        }
              | None ->
                    Seq.empty
