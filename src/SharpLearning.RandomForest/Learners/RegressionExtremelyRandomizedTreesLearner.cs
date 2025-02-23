﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using SharpLearning.Common.Interfaces;
using SharpLearning.Containers;
using SharpLearning.Containers.Matrices;
using SharpLearning.DecisionTrees.ImpurityCalculators;
using SharpLearning.DecisionTrees.Learners;
using SharpLearning.DecisionTrees.Models;
using SharpLearning.DecisionTrees.SplitSearchers;
using SharpLearning.DecisionTrees.TreeBuilders;
using SharpLearning.RandomForest.Models;

namespace SharpLearning.RandomForest.Learners;

/// <summary>
/// Learns a regression version of Extremely randomized trees
/// http://www.montefiore.ulg.ac.be/~ernst/uploads/news/id63/extremely-randomized-trees.pdf
/// </summary>
public sealed class RegressionExtremelyRandomizedTreesLearner : IIndexedLearner<double>, ILearner<double>
{
    readonly int m_trees;
    int m_featuresPrSplit;
    readonly int m_minimumSplitSize;
    readonly double m_subSampleRatio;
    readonly double m_minimumInformationGain;
    readonly int m_maximumTreeDepth;
    readonly Random m_random;
    readonly bool m_runParallel;

    /// <summary>
    /// The extremely randomized trees learner is an ensemble learner consisting of a series of randomized decision trees.
    /// It takes the randomization a step futher than random forest and also select the splits randomly
    /// </summary>
    /// <param name="trees">Number of trees to use in the ensemble</param>
    /// <param name="minimumSplitSize">The minimum size for a node to be split</param>
    /// <param name="maximumTreeDepth">The maximal tree depth before a leaf is generated</param>
    /// <param name="featuresPrSplit">Number of features used at each split in each tree</param>
    /// <param name="minimumInformationGain">The minimum improvement in information gain before a split is made</param>
    /// <param name="subSampleRatio">The ratio of observations sampled with replacement for each tree.
    /// Default is 1.0 sampling the same count as the number of observations in the input.
    /// If below 1.0 the algorithm changes to random patches</param>
    /// <param name="seed">Seed for the random number generator</param>
    /// <param name="runParallel">Use multi threading to speed up execution (default is true)</param>
    public RegressionExtremelyRandomizedTreesLearner(int trees = 100,
        int minimumSplitSize = 1,
        int maximumTreeDepth = 2000,
        int featuresPrSplit = 0,
        double minimumInformationGain = .000001,
        double subSampleRatio = 1.0,
        int seed = 42,
        bool runParallel = true)
    {
        if (trees < 1) { throw new ArgumentException("trees must be at least 1"); }
        if (featuresPrSplit < 0) { throw new ArgumentException("features pr split must be at least 1"); }
        if (minimumSplitSize <= 0) { throw new ArgumentException("minimum split size must be larger than 0"); }
        if (maximumTreeDepth <= 0) { throw new ArgumentException("maximum tree depth must be larger than 0"); }
        if (minimumInformationGain <= 0) { throw new ArgumentException("minimum information gain must be larger than 0"); }

        m_trees = trees;
        m_minimumSplitSize = minimumSplitSize;
        m_maximumTreeDepth = maximumTreeDepth;
        m_featuresPrSplit = featuresPrSplit;
        m_minimumInformationGain = minimumInformationGain;
        m_subSampleRatio = subSampleRatio;
        m_runParallel = runParallel;

        m_random = new Random(seed);
    }

    /// <summary>
    /// Learns a regression Extremely randomized trees model
    /// </summary>
    /// <param name="observations"></param>
    /// <param name="targets"></param>
    /// <returns></returns>
    public RegressionForestModel Learn(F64Matrix observations, double[] targets)
    {
        var indices = Enumerable.Range(0, targets.Length).ToArray();
        return Learn(observations, targets, indices);
    }

    /// <summary>
    /// Learns a regression extremely randomized trees model
    /// </summary>
    /// <param name="observations"></param>
    /// <param name="targets"></param>
    /// <param name="indices"></param>
    /// <returns></returns>
    public RegressionForestModel Learn(F64Matrix observations, double[] targets,
        int[] indices)
    {
        Checks.VerifyObservationsAndTargets(observations, targets);
        Checks.VerifyIndices(indices, observations, targets);

        if (m_featuresPrSplit == 0)
        {
            var count = (int)(observations.ColumnCount / 3.0);
            m_featuresPrSplit = count <= 0 ? 1 : count;
        }

        var results = new ConcurrentDictionary<int, RegressionDecisionTreeModel>();

        // Ensure each tree (index) is always created with the same random generator,
        // in both sequential and parallel mode.
        var treeIndex = 0;
        var treeIndexToRandomGenerators = Enumerable.Range(0, m_trees)
            .Select(v => new { Index = treeIndex++, Random = new Random(m_random.Next()) })
            .ToArray();

        if (!m_runParallel)
        {
            foreach (var indexToRandom in treeIndexToRandomGenerators)
            {
                var tree = CreateTree(observations, targets,
                    indices, indexToRandom.Random);

                results.TryAdd(indexToRandom.Index, tree);
            }
        }
        else
        {
            var rangePartitioner = Partitioner.Create(treeIndexToRandomGenerators, true);
            Parallel.ForEach(rangePartitioner, (indexToRandom, loopState) =>
            {
                var tree = CreateTree(observations, targets,
                    indices, indexToRandom.Random);

                results.TryAdd(indexToRandom.Index, tree);
            });
        }

        // Ensure the order of the trees.
        var models = results.OrderBy(v => v.Key).Select(v => v.Value).ToArray();
        var rawVariableImportance = VariableImportance(models, observations.ColumnCount);

        return new RegressionForestModel(models, rawVariableImportance);
    }

    /// <summary>
    /// Private explicit interface implementation for indexed learning.
    /// </summary>
    /// <param name="observations"></param>
    /// <param name="targets"></param>
    /// <param name="indices"></param>
    /// <returns></returns>
    IPredictorModel<double> IIndexedLearner<double>.Learn(
        F64Matrix observations, double[] targets, int[] indices) => Learn(observations, targets, indices);

    /// <summary>
    /// Private explicit interface implementation for indexed learning.
    /// </summary>
    /// <param name="observations"></param>
    /// <param name="targets"></param>
    /// <returns></returns>
    IPredictorModel<double> ILearner<double>.Learn(
        F64Matrix observations, double[] targets) => Learn(observations, targets);

    static double[] VariableImportance(RegressionDecisionTreeModel[] models, int numberOfFeatures)
    {
        var rawVariableImportance = new double[numberOfFeatures];

        foreach (var model in models)
        {
            var modelVariableImportance = model.GetRawVariableImportance();

            for (var j = 0; j < modelVariableImportance.Length; j++)
            {
                rawVariableImportance[j] += modelVariableImportance[j];
            }
        }
        return rawVariableImportance;
    }

    RegressionDecisionTreeModel CreateTree(F64Matrix observations, double[] targets,
        int[] indices, Random random)
    {
        var learner = new DecisionTreeLearner(
            new DepthFirstTreeBuilder(m_maximumTreeDepth,
                m_featuresPrSplit,
                m_minimumInformationGain,
                random.Next(),
                new RandomSplitSearcher(m_minimumSplitSize, random.Next()),
                new RegressionImpurityCalculator()));

        var treeIndicesLength = (int)Math.Round(m_subSampleRatio * (double)indices.Length);
        var treeIndices = new int[treeIndicesLength];

        for (var j = 0; j < treeIndicesLength; j++)
        {
            treeIndices[j] = indices[random.Next(indices.Length)];
        }

        var model = new RegressionDecisionTreeModel(learner.Learn(
            observations, targets, treeIndices));

        return model;
    }
}
