// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using TUnit.Core.Interfaces;

namespace PinataLabs.Typography.Tests
{
    /// <summary>
    /// Sets the AppContext switch <see cref="SwitchName"/> to <see cref="IsEnabled"/> before the test execution
    /// and resets the switch to its default value (i.e. <c>null</c>) after the test execution.
    /// </summary>
    /// <remarks>
    /// The switch is process-wide state. TUnit runs tests in parallel, so every test that uses this attribute
    /// must also be marked <c>[NotInParallel]</c> (without a key) so that no other test can observe the switch.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AppContextSwitchAttribute : Attribute, ITestStartEventReceiver, ITestEndEventReceiver
    {
        public AppContextSwitchAttribute(string switchName, bool isEnabled)
        {
            this.SwitchName = switchName;
            this.IsEnabled = isEnabled;
        }

        public string SwitchName { get; }

        public bool IsEnabled { get; }

        public int Order => 0;

        public ValueTask OnTestStart(TestContext context)
        {
            AppDomain.CurrentDomain.SetData(this.SwitchName, this.IsEnabled.ToString());
            return default;
        }

        public ValueTask OnTestEnd(TestContext context)
        {
            AppDomain.CurrentDomain.SetData(this.SwitchName, null);
            return default;
        }
    }
}
