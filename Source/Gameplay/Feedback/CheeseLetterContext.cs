using System;
using System.Collections.Generic;
using Verse;

namespace CheeseProtocol
{
    public static class CheeseLetterContext
    {
        private static Stack<CheeseRollTrace> _stack;

        public static bool Active => _stack != null && _stack.Count > 0;
        public static CheeseRollTrace Current => Active ? _stack.Peek() : null;

        public static void Push(CheeseRollTrace trace)
        {
            if (_stack == null) _stack = new Stack<CheeseRollTrace>();
            _stack.Push(trace);
        }

        public static void Pop()
        {
            if (!Active) return;
            _stack.Pop();
        }
    }
}