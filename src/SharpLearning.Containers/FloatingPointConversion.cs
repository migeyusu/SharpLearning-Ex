﻿using System;
using System.Globalization;

namespace SharpLearning.Containers;

public static class FloatingPointConversion
{
    public const string DefaultFormat = "R";

    public static readonly NumberFormatInfo Nfi = new();

    /// <summary>
    /// Default NumberStyle is Any.
    /// </summary>
    public static readonly NumberStyles NumberStyle = NumberStyles.Any;

    /// <summary>
    /// Default format for outputting double values to string.
    /// </summary>
    public static string ToString(double value)
    {
        return value.ToString(DefaultFormat, Nfi);
    }

    /// <summary>
    /// Default format for converting string values to double
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static double ToF64(string value)
    {
        return ToF64(value, ParseAnyNumberStyle);
    }

    /// <summary>
    /// Allows for custom conversion of string to double.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="converter"></param>
    /// <returns></returns>
    public static double ToF64(string value, Converter<string, double> converter)
    {
        return converter(value);
    }

    static double ParseAnyNumberStyle(string value)
    {
        return double.TryParse(value, NumberStyle, Nfi, out var result)
            ? result
            : throw new ArgumentException($"Unable to parse \"{value}\" to double");
    }
}
