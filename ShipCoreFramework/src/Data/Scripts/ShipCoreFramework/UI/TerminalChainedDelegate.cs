using System;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    /// <summary>
    /// Designed for appending custom conditions to Visible/Enabled of terminal controls or toolbar actions so that they can be hidden for specific conditions/subtypes/whatever.
    /// </summary>
    public class TerminalChainedDelegate
    {
        /// <summary>
        /// <paramref name="originalFunc"/> should always be the delegate this replaces, to properly chain with other mods doing the same.
        /// <para><paramref name="customFunc"/> should be your custom condition to append to the chain.</para>
        /// <para>As for <paramref name="checkOR"/>, leave false if you want to hide controls by returning false with your <paramref name="customFunc"/>.</para>
        /// <para>Otherwise set to true if you want to force-show otherwise hidden controls by returning true with your <paramref name="customFunc"/>.</para> 
        /// </summary>
        public static Func<IMyTerminalBlock, bool> Create(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc, bool checkOR = false)
        {
            return new TerminalChainedDelegate(originalFunc, customFunc, checkOR).ResultFunc;
        }

        private readonly Func<IMyTerminalBlock, bool> _originalFunc;
        private readonly Func<IMyTerminalBlock, bool> _customFunc;
        private readonly bool _checkOR;

        TerminalChainedDelegate(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc, bool checkOR)
        {
            _originalFunc = originalFunc;
            _customFunc = customFunc;
            _checkOR = checkOR;
        }

        bool ResultFunc(IMyTerminalBlock block)
        {
            if(block?.CubeGrid == null)
                return false;

            var originalCondition = (_originalFunc == null || _originalFunc.Invoke(block));
            var customCondition = (_customFunc == null || _customFunc.Invoke(block));

            if(_checkOR)
                return originalCondition || customCondition;
            else
                return originalCondition && customCondition;
        }
    }
}