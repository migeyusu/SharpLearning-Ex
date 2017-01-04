﻿using SharpLearning.Containers.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SharpLearning.Neural.Optimizers
{
    /// <summary>
    /// Neural net optimizer for controlling the weight updates in neural net learning.
    /// uses mini-batch stochastic gradient descent. 
    /// Several different optimization methods is available through the constructor.
    /// </summary>
    public sealed class NeuralNetOptimizer
    {
        readonly float m_learningRate;
        readonly float m_momentum;
        readonly int m_batchSize;

        readonly List<double[]> gsumWeights = new List<double[]>(); // last iteration gradients (used for momentum calculations)
        readonly List<double[]> xsumWeights = new List<double[]>(); // used in adam or adadelta

        readonly List<double[]> gsumBias = new List<double[]>(); // last iteration gradients (used for momentum calculations)
        readonly List<double[]> xsumBias = new List<double[]>(); // used in adam or adadelta

        readonly OptimizerMethod OptimizerMethod = OptimizerMethod.Sgd;
        readonly float m_ro = 0.95f;
        readonly float m_eps = 1e-6f;
        readonly float m_beta1 = 0.9f;
        readonly float m_beta2 = 0.999f;

        readonly float m_l1Decay = 0.0f;
        readonly float m_l2Decay = 0.0f;

        int m_iterationCounter; // iteration counter

        /// <summary>
        /// Neural net optimizer for controlling the weight updates in neural net learning.
        /// uses mini-batch stochastic gradient descent. 
        /// Several different optimization methods is available through the constructor.
        /// </summary>
        /// <param name="learningRate">Controls the step size when updating the weights. (Default is 0.01)</param>
        /// <param name="batchSize">Batch size for mini-batch stochastic gradient descent. (Default is 128)</param>
        /// <param name="l1decay">L1 reguralization term. (Default is 0, so no reguralization)</param>
        /// <param name="l2decay">L2 reguralization term. (Default is 0, so no reguralization)</param>
        /// <param name="optimizerMethod">The method used for optimization (Default is Adagrad)</param>
        /// <param name="momentum">Momentum for gradient update. Should be between 0 and 1. (Defualt is 0.9)</param>
        /// <param name="ro"></param>
        /// <param name="beta1">Exponential decay rate for estimates of first moment vector, should be in range 0 to 1 (Default is 0.9)</param>
        /// <param name="beta2">Exponential decay rate for estimates of second moment vector, should be in range 0 to 1 (Default is 0.999)</param>
        public NeuralNetOptimizer(double learningRate, int batchSize, double l1decay=0, double l2decay=0, 
            OptimizerMethod optimizerMethod = OptimizerMethod.Adagrad, double momentum = 0.9, double ro=0.95, double beta1=0.9, double beta2=0.999)
        {
            if (learningRate <= 0) { throw new ArgumentNullException("learning rate must be larger than 0. Was: " + learningRate); }
            if (batchSize <= 0) { throw new ArgumentNullException("batchSize must be larger than 0. Was: " + batchSize); }
            if (l1decay < 0) { throw new ArgumentNullException("l1decay must be positive. Was: " + l1decay); }
            if (l2decay < 0) { throw new ArgumentNullException("l1decay must be positive. Was: " + l2decay); }
            if (momentum <= 0) { throw new ArgumentNullException("momentum must be larger than 0. Was: " + momentum); }
            if (ro <= 0) { throw new ArgumentNullException("ro must be larger than 0. Was: " + ro); }
            if (beta1 <= 0) { throw new ArgumentNullException("beta1 must be larger than 0. Was: " + beta1); }
            if (beta2 <= 0) { throw new ArgumentNullException("beta2 must be larger than 0. Was: " + beta2); }

            m_learningRate = (float)learningRate;
            m_batchSize = batchSize;
            m_l1Decay = (float)l1decay;
            m_l2Decay = (float)l2decay;
            
            OptimizerMethod = optimizerMethod;
            m_momentum = (float)momentum;
            m_ro = (float)ro;
            m_beta1 = (float)beta1;
            m_beta2 = (float)beta2;
        }

        /// <summary>
        /// Updates the parameters based on stochastic gradient descent.
        /// </summary>
        /// <param name="parametersAndGradients"></param>
        public void UpdateParameters(List<ParametersAndGradients> parametersAndGradients)
        {
            m_iterationCounter++;
            
            // initialize accumulators. Will only be done once on first iteration and if optimizer methods is not sgd
            var useAccumulators = gsumWeights.Count == 0 && (OptimizerMethod != OptimizerMethod.Sgd || m_momentum > 0.0);
            if (useAccumulators) { InitializeAccumulators(parametersAndGradients); }

            // perform update of all parameters
            Parallel.For(0, parametersAndGradients.Count, i => 
            {
                var parametersAndGradient = parametersAndGradients[i];
                
                // extract parameters and gradients
                var parameters = parametersAndGradient.Parameters.Weights.Data();
                var parametersBias = parametersAndGradient.Parameters.Bias.Data();
                var gradients = parametersAndGradient.Gradients.Weights.Data();
                var gradientsBias = parametersAndGradient.Gradients.Bias.Data();

                // update weights
                UpdateParam(i, parameters, gradients, m_l2Decay, m_l1Decay, gsumWeights, xsumWeights);
                    
                // Update biases
                UpdateParam(i, parametersBias, gradientsBias, m_l2Decay, m_l1Decay, gsumBias, xsumBias);
            });
        }

        void InitializeAccumulators(List<ParametersAndGradients> parametersAndGradients)
        {
            for (var i = 0; i < parametersAndGradients.Count; i++)
            {
                gsumWeights.Add(new double[parametersAndGradients[i].Parameters.Weights.Data().Length]);
                gsumBias.Add(new double[parametersAndGradients[i].Parameters.Bias.Data().Length]);
                if (OptimizerMethod == OptimizerMethod.Adam || OptimizerMethod == OptimizerMethod.Adadelta)
                {
                    xsumWeights.Add(new double[parametersAndGradients[i].Parameters.Weights.Data().Length]);
                    xsumBias.Add(new double[parametersAndGradients[i].Parameters.Bias.Data().Length]);
                }
            }
        }

        private void UpdateParam(int i, float[] parameters, float[] gradients, double l2Decay, double l1Decay,
            List<double[]> gsum, List<double[]> xsum)
        {
            for (var j = 0; j < parameters.Length; j++)
            {
                var l1Grad = l1Decay * (parameters[j] > 0 ? 1 : -1);
                var l2Grad = l2Decay * (parameters[j]);

                var gij = (l2Grad + l1Grad + gradients[j]) / m_batchSize; // raw batch gradient

                double[] gsumi = null;
                if (gsum.Count > 0)
                {
                    gsumi = gsum[i];
                }

                double[] xsumi = null;
                if (xsum.Count > 0)
                {
                    xsumi = xsum[i];
                }

                switch (OptimizerMethod)
                {
                    case OptimizerMethod.Sgd:
                        {
                            if (m_momentum > 0.0) // sgd + momentum
                            {
                                var dx = m_momentum * gsumi[j] - m_learningRate * gij; // step
                                gsumi[j] = dx; // back this up for next iteration of momentum
                                parameters[j] += (float)dx; // apply corrected gradient
                            }
                            else // standard sgd
                            {
                                parameters[j] += (float)(-m_learningRate * gij);
                            }
                        }
                        break;
                    case OptimizerMethod.Adam:
                        {
                            gsumi[j] = gsumi[j] * m_beta1 + (1 - m_beta1) * gij; // update biased first moment estimate
                            xsumi[j] = xsumi[j] * m_beta2 + (1 - m_beta2) * gij * gij; // update biased second moment estimate
                            var biasCorr1 = gsumi[j] * (1 - Math.Pow(m_beta1, m_iterationCounter)); // correct bias first moment estimate
                            var biasCorr2 = xsumi[j] * (1 - Math.Pow(m_beta2, m_iterationCounter)); // correct bias second moment estimate
                            var dx = -m_learningRate * biasCorr1 / (Math.Sqrt(biasCorr2) + m_eps);
                            parameters[j] += (float)dx;
                        }
                        break;
                    case OptimizerMethod.Adagrad:
                        {
                            gsumi[j] = gsumi[j] + gij * gij;
                            var dx = -m_learningRate / Math.Sqrt(gsumi[j] + m_eps) * gij;
                            parameters[j] += (float)dx;
                        }
                        break;
                    case OptimizerMethod.Adadelta:
                        {
                            gsumi[j] = m_ro * gsumi[j] + (1 - m_ro) * gij * gij;
                            var dx = -Math.Sqrt((xsumi[j] + m_eps) / (gsumi[j] + m_eps)) * gij;
                            xsumi[j] = m_ro * xsumi[j] + (1 - m_ro) * dx * dx;
                            parameters[j] += (float)dx;
                        }
                        break;
                    case OptimizerMethod.Windowgrad:
                        {
                            gsumi[j] = m_ro * gsumi[j] + (1 - m_ro) * gij * gij;
                            var dx = -m_learningRate / Math.Sqrt(gsumi[j] + m_eps) * gij;
                            parameters[j] += (float)dx;
                        }
                        break;
                    case OptimizerMethod.Netsterov:
                        {
                            var dx = gsumi[j];
                            gsumi[j] = gsumi[j] * m_momentum + m_learningRate * gij;
                            dx = m_momentum * dx - (1.0 + m_momentum) * gsumi[j];
                            parameters[j] += (float)dx;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                gradients[j] = 0.0f; // zero out gradient between each iteration
            }
        }

        /// <summary>
        /// Resets the counters and momentum sums.
        /// </summary>
        public void Reset()
        {
            // clear counter
            m_iterationCounter = 0;
            
            // clear sums
            for (int i = 0; i < gsumWeights.Count; i++)
            {
                gsumWeights[i].Clear();
                gsumBias[i].Clear();
                xsumWeights[i].Clear();
                xsumBias[i].Clear();
            }
        }
    }
}