﻿using System;
using System.Collections.Generic;
using System.Reflection;
using Bddify.Core;
using System.Linq;

namespace Bddify.Scanners.StepScanners.MethodName
{
    public class MethodNameStepScanner : IScanForSteps
    {
        private readonly MethodNameMatcher[] _matchers;

        public MethodNameStepScanner(MethodNameMatcher[] matchers)
        {
            _matchers = matchers;
        }

        public int Priority
        {
            get { return 20; }
        }

        public IEnumerable<ExecutionStep> Scan(object testObject)
        {
            var scenarioType = testObject.GetType();
            var methodsToScan = scenarioType.GetMethodsOfInterest();
            var foundMethods = new List<MethodInfo>();

            foreach (var matcher in _matchers)
            {
                // if a method is already matched we should exclude it because it may match against another criteria too
                // e.g. a method starting with AndGiven matches against both AndGiven and And
                foreach (var method in methodsToScan.Except(foundMethods))
                {
                    if (!matcher.IsMethodOfInterest(method.Name)) 
                        continue;

                    foundMethods.Add(method);

                    var argAttributes = (RunStepWithArgsAttribute[])method.GetCustomAttributes(typeof(RunStepWithArgsAttribute), false);
                    var returnsItsText = method.ReturnType == typeof(IEnumerable<string>);

                    if (argAttributes.Length == 0)
                    {
                        yield return GetStep(testObject, matcher, method, returnsItsText);
                        continue;
                    }

                    foreach (var argAttribute in argAttributes)
                    {
                        var inputs = argAttribute.InputArguments;
                        if (inputs != null && inputs.Length > 0)
                            yield return GetStep(testObject, matcher, method, returnsItsText, inputs, argAttribute);
                    }
                }
            }
        }

        private static ExecutionStep GetStep(object testObject, MethodNameMatcher matcher, MethodInfo method, bool returnsItsText, object[] inputs = null, RunStepWithArgsAttribute argAttribute = null)
        {
            var stepMethodName = GetStepTitle(method, testObject, argAttribute, returnsItsText);
            var stepAction = GetStepAction(method, inputs, returnsItsText);
            return new ExecutionStep(stepAction, stepMethodName, matcher.Asserts, matcher.ExecutionOrder, matcher.ShouldReport);
        }

        // ToDo: this method does not belong to this class. I need to do a serious clean up in step scanners
        private static Action<object> GetStepAction(MethodInfo method, object[] inputs, bool returnsItsText)
        {
            if (returnsItsText)
            {
                // Note: Count() is a silly trick to enumerate over the method and make sure it returns because it is an IEnumerable method and runs lazily otherwise
                return o => InvokeIEnumerableMethod(method, o, inputs).Count();
            }

            return o => method.Invoke(o, inputs);
        }

        private static string GetStepTitle(MethodInfo method, object testObject, RunStepWithArgsAttribute argAttribute, bool returnsItsText)
        {
            Func<string> stepTitleFromMethodName = () => GetStepTitleFromMethodName(method, argAttribute);

            if(returnsItsText)
                return GetStepTitleFromMethod(method, argAttribute, testObject) ?? stepTitleFromMethodName();

            return stepTitleFromMethodName();
        }

        private static string GetStepTitleFromMethodName(MethodInfo method, RunStepWithArgsAttribute argAttribute)
        {
            var methodName = CleanupTheStepText(NetToString.Convert(method.Name));
            object[] inputs = null;

            if (argAttribute != null && argAttribute.InputArguments != null)
                inputs = argAttribute.InputArguments;

            if (inputs == null)
                return methodName;
            
            if (string.IsNullOrEmpty(argAttribute.StepTextTemplate))
                return methodName + " " + string.Join(", ", inputs.FlattenArrays());

            return string.Format(argAttribute.StepTextTemplate, inputs.FlattenArrays());
        }

        private static string GetStepTitleFromMethod(MethodInfo method, RunStepWithArgsAttribute argAttribute, object testObject)
        {
            object[] inputs = null;
            if(argAttribute != null && argAttribute.InputArguments != null)
                inputs = argAttribute.InputArguments;

            var enumerableResult = InvokeIEnumerableMethod(method, testObject, inputs);
            try
            {
                return enumerableResult.FirstOrDefault();
            }
            catch (Exception ex)
            {
                var message = string.Format(
                    "The signature of method '{0}' indicates that it returns its step title; but the code is throwing an exception before a title is returned",
                    method.Name);
                throw new StepTitleException(message, ex);
            }
        }

        private static IEnumerable<string> InvokeIEnumerableMethod(MethodInfo method, object testObject, object[] inputs)
        {
            return (IEnumerable<string>)method.Invoke(testObject, inputs);
        }

        static string CleanupTheStepText(string stepText)
        {
            if (stepText.StartsWith("and given ", StringComparison.OrdinalIgnoreCase))
                return stepText.Remove("and ".Length, "given ".Length);

            if(stepText.StartsWith("and when ", StringComparison.OrdinalIgnoreCase))
                return stepText.Remove("and ".Length, "when ".Length);

            return stepText;
        }
    }
}