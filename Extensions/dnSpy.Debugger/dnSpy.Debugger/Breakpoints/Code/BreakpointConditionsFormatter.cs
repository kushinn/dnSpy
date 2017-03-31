﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using dnSpy.Contracts.Debugger.Breakpoints.Code;
using dnSpy.Contracts.Text;
using dnSpy.Debugger.Properties;

namespace dnSpy.Debugger.Breakpoints.Code {
	abstract class BreakpointConditionsFormatter {
		public abstract void Write(ITextColorWriter output, DbgCodeBreakpointCondition? condition);
		public abstract void Write(ITextColorWriter output, DbgCodeBreakpointHitCount? hitCount);
		public abstract void Write(ITextColorWriter output, DbgCodeBreakpointFilter? filter);
		public abstract void Write(ITextColorWriter output, DbgCodeBreakpointTrace? trace);
	}

	[Export(typeof(BreakpointConditionsFormatter))]
	sealed class BreakpointConditionsFormatterImpl : BreakpointConditionsFormatter {
		public override void Write(ITextColorWriter output, DbgCodeBreakpointCondition? condition) {
			if (output == null)
				throw new ArgumentNullException(nameof(output));
			if (condition == null)
				output.Write(BoxedTextColor.Text, dnSpy_Debugger_Resources.Breakpoint_Condition_NoCondition);
			else {
				switch (condition.Value.Kind) {
				case DbgCodeBreakpointConditionKind.IsTrue:
					WriteArgumentAndText(output, BoxedTextColor.String, dnSpy_Debugger_Resources.Breakpoint_Condition_WhenConditionIsTrue, condition.Value.Condition);
					break;

				case DbgCodeBreakpointConditionKind.WhenChanged:
					WriteArgumentAndText(output, BoxedTextColor.String, dnSpy_Debugger_Resources.Breakpoint_Condition_WhenConditionHasChanged, condition.Value.Condition);
					break;

				default:
					Debug.Fail($"Unknown kind: {condition.Value.Kind}");
					break;
				}
			}
		}

		public override void Write(ITextColorWriter output, DbgCodeBreakpointHitCount? hitCount) {
			if (output == null)
				throw new ArgumentNullException(nameof(output));
			if (hitCount == null)
				output.Write(BoxedTextColor.Text, dnSpy_Debugger_Resources.Breakpoint_HitCount_NoHitCount);
			else {
				switch (hitCount.Value.Kind) {
				case DbgCodeBreakpointHitCountKind.Equals:
					WriteArgumentAndText(output, BoxedTextColor.Number, dnSpy_Debugger_Resources.Breakpoint_HitCount_HitCountIsEqualTo, hitCount.Value.Count.ToString());
					break;

				case DbgCodeBreakpointHitCountKind.MultipleOf:
					WriteArgumentAndText(output, BoxedTextColor.Number, dnSpy_Debugger_Resources.Breakpoint_HitCount_HitCountIsAMultipleOf, hitCount.Value.Count.ToString());
					break;

				case DbgCodeBreakpointHitCountKind.GreaterThanOrEquals:
					WriteArgumentAndText(output, BoxedTextColor.Number, dnSpy_Debugger_Resources.Breakpoint_HitCount_HitCountIsGreaterThanOrEqualTo, hitCount.Value.Count.ToString());
					break;

				default:
					Debug.Fail($"Unknown kind: {hitCount.Value.Kind}");
					break;
				}
			}
		}

		public override void Write(ITextColorWriter output, DbgCodeBreakpointFilter? filter) {
			if (output == null)
				throw new ArgumentNullException(nameof(output));
			if (filter == null)
				output.Write(BoxedTextColor.Text, dnSpy_Debugger_Resources.Breakpoint_Filter_NoFilter);
			else
				output.Write(BoxedTextColor.String, filter.Value.Filter ?? string.Empty);
		}

		public override void Write(ITextColorWriter output, DbgCodeBreakpointTrace? trace) {
			if (output == null)
				throw new ArgumentNullException(nameof(output));
			if (trace == null)
				output.Write(BoxedTextColor.Text, dnSpy_Debugger_Resources.Breakpoint_Tracepoint_NoTraceMessage);
			else
				WriteArgumentAndText(output, BoxedTextColor.String, dnSpy_Debugger_Resources.Breakpoint_Tracepoint_PrintMessage, trace.Value.Message);
		}

		void WriteArgumentAndText(ITextColorWriter output, object valueColor, string formatString, string formatValue) {
			if (formatValue == null)
				formatValue = string.Empty;
			var defaultColor = BoxedTextColor.Comment;
			const string pattern = "{0}";
			var index = formatString.IndexOf(pattern);
			if (index < 0) {
				Debug.Fail("Couldn't find the sub string");
				output.Write(defaultColor, string.Format(formatString, formatValue));
			}
			else {
				if (index != 0)
					output.Write(defaultColor, formatString.Substring(0, index));
				output.Write(valueColor, string.Format(pattern, formatValue));
				if (index + pattern.Length != formatString.Length)
					output.Write(defaultColor, formatString.Substring(index + pattern.Length));
			}
		}
	}
}