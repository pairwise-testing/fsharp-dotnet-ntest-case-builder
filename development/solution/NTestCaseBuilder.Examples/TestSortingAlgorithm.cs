﻿using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NTestCaseBuilder.Examples
{
    ///<summary>
    ///  Test fixture for class 'SortingAlgorithmModule'.
    ///</summary>
    [TestFixture]
    public class TestSortingAlgorithm
    {
        ///<summary>
        ///  A test case to apply sorting to. Provides a sequence of integers in some unspecified order
        ///  - may or may not be sorted in ascending order. Some of the integers in the sequence may be
        ///  duplicated; the duplicates may or may not be adjacent to each other.
        ///  The sequence is generated by permuting a sequence of integers that is known by construction
        ///  to be monotonic increasing, with any duplicates arranged into runs of adjacent duplicated
        ///  values. This base sequence is also made available to check the expected results from any sorting
        ///  algorithm.
        ///</summary>
        public class TestCase
        {
            ///<summary>
            ///  Constructor for use by synthesizing factory.
            ///</summary>
            ///<param name = "leastItemInSequence">The lowest value that starts off <cref>OriginalMonotonicIncreasingSequence</cref></param>
            ///<param name = "nonNegativeDeltas">Sequence of non-negative deltas that will be used to build up <cref>OriginalMonotonicIncreasingSequence</cref></param>
            ///<param name = "permutation">A permutation that is used to shuffle <cref>OriginalMonotonicIncreasingSequence</cref> to give <cref>PermutedSequence</cref></param>
            public TestCase(Int32 leastItemInSequence, IEnumerable<Int32> nonNegativeDeltas,
                            Permutation<Int32> permutation)
            {
                var originalMonotonicIncreasingSequence = new List<Int32>();

                var runningSum = leastItemInSequence;

                foreach (var nonNegativeDelta in nonNegativeDeltas)
                {
                    originalMonotonicIncreasingSequence.Add(runningSum);
                    runningSum += (Int32) nonNegativeDelta;
                }

                originalMonotonicIncreasingSequence.Add(runningSum);

                OriginalMonotonicIncreasingSequence = originalMonotonicIncreasingSequence;

                PermutedSequence = permutation(originalMonotonicIncreasingSequence);
            }

            ///<summary>
            ///  Parameterless constructor that represents the trivial empty sequence case.
            ///</summary>
            public TestCase()
            {
                OriginalMonotonicIncreasingSequence = new List<Int32>();

                PermutedSequence = new List<Int32>();
            }

            /// <summary>
            ///   The sequence to be used as input to a sorting algorithm.
            /// </summary>
            public IEnumerable<Int32> PermutedSequence { get; set; }

            ///<summary>
            ///  The expected result of sorting <cref>PermutedSequence</cref>.
            ///</summary>
            public IEnumerable<Int32> OriginalMonotonicIncreasingSequence { get; set; }
        }

        private static ITypedFactory<TestCase> BuildTestCaseFactory()
        {
            var factoryForLeastItemInSequence = TestVariable.Create(Enumerable.Range(-3, 10));

            const int maximumNumberOfDeltas = 4;

            var factoryForNonNegativeDeltasAndPermutation =
                Interleaving.Create(from numberOfDeltas in Enumerable.Range(0, 1 + maximumNumberOfDeltas)
                                    select BuildNonNegativeDeltasAndPermutationFactory(numberOfDeltas));

            var testCaseFactoryForTrivialCase = Singleton.Create(new TestCase());

            var testCaseFactoryForNonTrivialCases = Synthesis.Create(factoryForLeastItemInSequence,
                                                                     factoryForNonNegativeDeltasAndPermutation,
                                                                     (leastItemInSequence,
                                                                      nonNegativeDeltasAndItsPermutation) =>
                                                                     new TestCase(leastItemInSequence,
                                                                                  nonNegativeDeltasAndItsPermutation.
                                                                                      Item1,
                                                                                  nonNegativeDeltasAndItsPermutation.
                                                                                      Item2));

            return Interleaving.Create(new[] {testCaseFactoryForTrivialCase, testCaseFactoryForNonTrivialCases});
        }

        private static ITypedFactory<Tuple<IEnumerable<Int32>, Permutation<Int32>>>
            BuildNonNegativeDeltasAndPermutationFactory(int numberOfDeltas)
        {
            var factoryForNonNegativeDelta =
                TestVariable.Create(from signedDelta in Enumerable.Range(0, 5) select (Int32) signedDelta);
            return
                Synthesis.CreateWithPermutation<Int32, Int32>(Enumerable.Repeat(factoryForNonNegativeDelta,
                                                                                numberOfDeltas));
        }

        ///<summary>
        ///  Parameterised unit test for <cref>SortingAlgorithmModule.SortWithBug</cref>.
        ///</summary>
        ///<remarks>
        ///  This is expected to fail.
        ///</remarks>
        ///<param name = "testCase"></param>
        public static void
            ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm(
            TestCase testCase)
        {
            Console.WriteLine("[{0}]", String.Join(", ", testCase.PermutedSequence));

            var sortedSequence = SortingAlgorithmModule.SortWithBug(testCase.PermutedSequence);

            Assert.IsTrue(sortedSequence.SequenceEqual(testCase.OriginalMonotonicIncreasingSequence));
        }

        ///<summary>
        ///  Parameterised unit test for <cref>SortingAlgorithmModule.SortThatWorks</cref>.
        ///</summary>
        ///<remarks>
        ///  This is expected to succeed.
        ///</remarks>
        ///<param name = "testCase"></param>
        public static void
            ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByCorrectSortingAlgorithm(
            TestCase testCase)
        {
            Console.WriteLine("[{0}]", String.Join(", ", testCase.PermutedSequence));

            var sortedSequence = SortingAlgorithmModule.SortThatWorks(testCase.PermutedSequence);

            Assert.IsTrue(sortedSequence.SequenceEqual(testCase.OriginalMonotonicIncreasingSequence));
        }

        ///<summary>
        ///  Unit test for <cref>SortingAlgorithmModule.SortWithBug</cref>.
        ///</summary>
        [Test]
        public void TestReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm()
        {
            var factory = BuildTestCaseFactory();
            const Int32 strength = 3;

            var howManyTestCasesWereExecuted = factory.ExecuteParameterisedUnitTestForAllTestCases(strength,
                                                                                                   ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm);

            Console.WriteLine("Executed {0} test cases successfully.", howManyTestCasesWereExecuted);
        }

        ///<summary>
        ///  Unit test for <cref>SortingAlgorithmModule.SortWithBug</cref>.
        ///</summary>
        [Test]
        public void TestReassemblyOfPermutedMonotonicIncreasingSequenceByCorrectSortingAlgorithm()
        {
            var factory = BuildTestCaseFactory();
            const Int32 strength = 3;

            var howManyTestCasesWereExecuted = factory.ExecuteParameterisedUnitTestForAllTestCases(strength,
                                                                                                   ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByCorrectSortingAlgorithm);

            Console.WriteLine("Executed {0} test cases successfully.", howManyTestCasesWereExecuted);
        }

        ///<summary>
        ///  Reproduce the test failure from <cref>TestReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm</cref>.
        ///</summary>
        [Test]
        public void TestThatQuicklyReproducesTheFailureFromTheBuggyTest()
        {
            const string reproduction =
                // This is cut and paste from the exception thrown by test TestReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm.
                "1090764779116129690923515858308014520222336185700694896976936400046940578111983112055989629000774433035533486068550533022050440563758532034744094390335385597493640149399285518641151929556092665584402288546355440347730368088836771627466259556412021922627725184177788154110107097070732172385860911454891134069325355752405254360949593534879521837273025056195093106165870371041268817411831127077924824920514034566098697648181040760873045194504973952223951724813225504499983047118862663287945210705883307688057054025916951667043265570775146944395249011036178056877575401364350157809719147995239162387828809111641197498761314743201387695694648950056523648142393681967482016898244952046671875614901026969586459303447635896866101123266210751713646802033809707609168906792115491183477974610728150992273068596408688241773251963923138429130520691098243248893419892952443542052191199628809010572675447824551593304497482463813697355082304187386823950217095809276435625749534789299526459222114593592266686209126390984795821501381781745708132091435924939833210114218265084665947358428911669581585498376927407493552466725446760623701467939560074187223831230919745620371755213508831636067167684360039211127262835836349843341641597785732280357370537268744363822886739938694773173734590922835035954079750112107318950146026524038408134904444586730532951131436902225568662170090521600282819666798575526991783952385901520151082949249355302351810143145633498325010249964328245271752986380701194391795177417832062797941842182732805832732335710897274060061770940391780122474565229191756062364516816599581273317348176499228496927992511523072853252075332731890715264392205033020620778478135070528984576870458536756429551045411434100804134681784802530326234271197330772618947349597030925327266034680958734216931873495784114303377681612871232138727564369215981205645526032669238082470024868794218096297605262460037102638172004995808152171391862902260941117558050337516270511463749491604762977109930535373246706986172899428371745042253317918107906121040873486737512994890357293396689164960629846996988387599881819260138835140665303070639472747816265470162847876957484029766979691843665957532773395930568939163631862895529691637676354435952393085610138558008072722760071077361553157249091500252449648886446114577130960581145642226501720172722101650580698819067818352781376634324409182402883078422358854325278360944098195948662827735052082866490169948823637943612256334313998270759439470885372690432";

            var factory = BuildTestCaseFactory();

            factory.ExecuteParameterisedUnitTestForReproducedTestCase(
                ParameterisedUnitTestForReassemblyOfPermutedMonotonicIncreasingSequenceByBuggySortingAlgorithm,
                reproduction);
        }
    }
}