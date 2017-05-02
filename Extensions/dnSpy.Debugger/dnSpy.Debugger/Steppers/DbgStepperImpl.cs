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
using System.Diagnostics;
using dnSpy.Contracts.Debugger;
using dnSpy.Contracts.Debugger.Engine.Steppers;
using dnSpy.Contracts.Debugger.Steppers;
using dnSpy.Debugger.Impl;

namespace dnSpy.Debugger.Steppers {
	sealed class DbgStepperImpl : DbgStepper {
		public override DbgThread Thread => thread;
		public override bool CanStep => !IsClosed && thread.Process.State == DbgProcessState.Paused && !IsStepping;
		public override event EventHandler<DbgStepCompleteEventArgs> StepComplete;
		DbgDispatcher Dispatcher => Thread.Process.DbgManager.Dispatcher;

		public override bool IsStepping {
			get {
				lock (lockObj)
					return stepperTag != null;
			}
		}

		internal DbgEngineStepper EngineStepper => engineStepper;

		readonly object lockObj;
		readonly DbgManagerImpl dbgManager;
		readonly DbgThreadImpl thread;
		readonly DbgEngineStepper engineStepper;
		object stepperTag;
		bool closeWhenStepComplete;

		public DbgStepperImpl(DbgManagerImpl dbgManager, DbgThreadImpl thread, DbgEngineStepper engineStepper) {
			lockObj = new object();
			this.dbgManager = dbgManager ?? throw new ArgumentNullException(nameof(dbgManager));
			this.thread = thread ?? throw new ArgumentNullException(nameof(thread));
			this.engineStepper = engineStepper ?? throw new ArgumentNullException(nameof(engineStepper));
			thread.AddAutoClose(this);
			engineStepper.StepComplete += DbgEngineStepper_StepComplete;
		}

		void DbgEngineStepper_StepComplete(object sender, DbgEngineStepCompleteEventArgs e) =>
			Dispatcher.BeginInvoke(() => DbgEngineStepper_StepComplete_DbgThread(e));

		void DbgEngineStepper_StepComplete_DbgThread(DbgEngineStepCompleteEventArgs e) {
			Dispatcher.VerifyAccess();
			bool wasStepping;
			lock (lockObj) {
				wasStepping = stepperTag != null && stepperTag == e.Tag;
				stepperTag = null;
			}
			dbgManager.StepComplete_DbgThread((DbgThreadImpl)e.Thread ?? thread, e.Error);
			if (wasStepping)
				RaiseStepComplete_DbgThread(e.Error);
		}

		void RaiseStepComplete(string error) => Dispatcher.BeginInvoke(() => RaiseStepComplete_DbgThread(error));

		void RaiseStepComplete_DbgThread(string error) {
			Dispatcher.VerifyAccess();
			StepComplete?.Invoke(this, new DbgStepCompleteEventArgs(error));
			if (closeWhenStepComplete)
				Close(Dispatcher);
		}

		internal void RaiseError_DbgThread(string error) {
			Dispatcher.VerifyAccess();
			if (error == null)
				throw new ArgumentNullException(nameof(error));
			bool wasStepping;
			lock (lockObj) {
				wasStepping = stepperTag != null;
				stepperTag = null;
			}
			if (wasStepping)
				RaiseStepComplete_DbgThread(error);
		}

		public override void Step(DbgStepperKind step, bool autoClose) {
			Debug.Assert(CanStep);
			if (IsClosed) {
				Debug.Fail("Stepper has been closed");
				// No need to localize it, if we're here it's a bug that should be fixed
				RaiseStepComplete("Stepper has been closed");
				return;
			}

			if (thread.Process.State != DbgProcessState.Paused) {
				Debug.Fail("Process isn't paused");
				// No need to localize it, if we're here it's a bug that should be fixed
				RaiseStepComplete("Process isn't paused");
				return;
			}

			bool canStep;
			object stepperTagTmp;
			lock (lockObj) {
				canStep = stepperTag == null;
				if (canStep)
					stepperTag = new object();
				stepperTagTmp = stepperTag;
			}
			if (!canStep) {
				Debug.Fail("Previous step isn't complete");
				// No need to localize it, if we're here it's a bug that should be fixed
				RaiseStepComplete("Previous step isn't complete");
				return;
			}

			closeWhenStepComplete = autoClose;
			switch (step) {
			case DbgStepperKind.StepInto:
				dbgManager.Step(this, stepperTagTmp, DbgEngineStepperKind.StepInto, singleProcess: false);
				break;

			case DbgStepperKind.StepOver:
				dbgManager.Step(this, stepperTagTmp, DbgEngineStepperKind.StepOver, singleProcess: false);
				break;

			case DbgStepperKind.StepOut:
				dbgManager.Step(this, stepperTagTmp, DbgEngineStepperKind.StepOut, singleProcess: false);
				break;

			case DbgStepperKind.StepIntoProcess:
				dbgManager.Step(this, stepperTagTmp, DbgEngineStepperKind.StepInto, singleProcess: true);
				break;

			case DbgStepperKind.StepOverProcess:
				dbgManager.Step(this, stepperTagTmp, DbgEngineStepperKind.StepOver, singleProcess: true);
				break;

			case DbgStepperKind.StepOutProcess:
				dbgManager.Step(this, stepperTagTmp, DbgEngineStepperKind.StepOut, singleProcess: true);
				break;

			default:
				lock (lockObj)
					stepperTag = null;
				throw new ArgumentOutOfRangeException(nameof(step));
			}
		}

		public override void Cancel() {
			object stepperTagTmp;
			lock (lockObj) {
				if (stepperTag == null)
					return;
				stepperTagTmp = stepperTag;
				stepperTag = null;
			}
			engineStepper.Cancel(stepperTagTmp);
		}

		public override void Close() => Thread.Process.DbgManager.Close(this);

		protected override void CloseCore() {
			Dispatcher.VerifyAccess();
			thread.RemoveAutoClose(this);
			engineStepper.StepComplete -= DbgEngineStepper_StepComplete;
			engineStepper.Close(Dispatcher);
		}
	}
}