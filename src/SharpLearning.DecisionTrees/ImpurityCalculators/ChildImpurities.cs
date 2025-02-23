﻿using System;

namespace SharpLearning.DecisionTrees.ImpurityCalculators;

/// <summary>
/// Struct for containing left and right child impurities
/// </summary>
public struct ChildImpurities : IEquatable<ChildImpurities>
{
    /// <summary>
    ///
    /// </summary>
    public readonly double Left;

    /// <summary>
    ///
    /// </summary>
    public readonly double Right;

    /// <summary>
    ///
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    public ChildImpurities(double left, double right)
    {
        Left = left;
        Right = right;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Equals(ChildImpurities other)
    {
        if (!Equal(Left, other.Left)) { return false; }
        return Equal(Right, other.Right);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public override bool Equals(object obj)
    {
        return obj is ChildImpurities impurities && Equals(impurities);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <returns></returns>
    public static bool operator ==(ChildImpurities p1, ChildImpurities p2)
    {
        return p1.Equals(p2);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <returns></returns>
    public static bool operator !=(ChildImpurities p1, ChildImpurities p2)
    {
        return !p1.Equals(p2);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
    {
        return Left.GetHashCode() ^ Right.GetHashCode();
    }

    const double Tolerence = 0.00001;

    static bool Equal(double a, double b)
    {
        var diff = Math.Abs(a * Tolerence);
        return Math.Abs(a - b) <= diff;
    }
}
