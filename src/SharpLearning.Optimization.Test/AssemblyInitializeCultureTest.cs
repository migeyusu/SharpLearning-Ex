﻿using System.Globalization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpLearning.Optimization.Test;

[TestClass]
public static class AssemblyInitializeCultureTest
{
    [AssemblyInitialize]
    public static void AssemblyInitializeCultureTest_InvariantCulture(TestContext c)
    {
        CultureInfo culture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
