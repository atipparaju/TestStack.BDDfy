﻿using System.Collections.Generic;
using System.Linq;
using Bddify.Core;
using Bddify.Scanners.StepScanners;
using Bddify.Scanners.ScenarioScanners;

namespace Bddify.Tests.Scanner
{
    internal static class StepScannerExtensions
    {
        internal static IList<ExecutionStep> Scan(this IStepScanner scanner, object testObject)
        {
            // ToDo: this is rather hacky and is not DRY. Should think of a way to get rid of this
            return ReflectiveScenarioScanner
                .GetMethodsOfInterest(testObject.GetType())
                .SelectMany(scanner.Scan)
                .OrderBy(s => s.ExecutionOrder)
                .ToList();
        }
    }
}
