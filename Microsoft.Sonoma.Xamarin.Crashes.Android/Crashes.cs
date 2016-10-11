﻿using Android.Runtime;
using Android.Util;
using Com.Microsoft.Sonoma.Crashes;
using Com.Microsoft.Sonoma.Crashes.Ingestion.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.Sonoma.Xamarin.Crashes
{
    using AndroidCrashes = Com.Microsoft.Sonoma.Crashes.Crashes;
    using Exception = System.Exception;
    using ModelException = Com.Microsoft.Sonoma.Crashes.Ingestion.Models.Exception;
    using ModelStackFrame = Com.Microsoft.Sonoma.Crashes.Ingestion.Models.StackFrame;

    public static class Crashes
    {
        public static Type BindingType => typeof(AndroidCrashes);

        public static bool Enabled
        {
            get { return AndroidCrashes.Enabled; }
            set { AndroidCrashes.Enabled = value; }
        }

        private static readonly ModelStackFrame EmptyModelFrame = new ModelStackFrame();

        private static ManagedErrorLog _errorLog;

        private static Exception _exception;

        static Crashes()
        {
            Log.Info("SonomaXamarin", "Set up Xamarin crash handler.");
            AndroidEnvironment.UnhandledExceptionRaiser += OnUnhandledException;
            AndroidCrashes.SetListener(new CrashListener());
        }

        private static void OnUnhandledException(object sender, RaiseThrowableEventArgs e)
        {
            _exception = e.Exception;
            Log.Error("SonomaXamarin", "Xamarin crash " + _exception);
            JoinExceptionAndLog();
        }

        private static void JoinExceptionAndLog()
        {
            if (_errorLog != null && _exception != null)
            {
                _errorLog.Exception = GenerateModelException();
                AndroidCrashes.Instance.SaveWrapperSdkErrorLog(_errorLog);
            }
        }

        private static ModelException GenerateModelException()
        {
            ModelException topException = null;
            ModelException parentException = null;
            for (var cause = _exception; cause != null; cause = cause.InnerException)
            {
                var exception = new ModelException
                {
                    Type = cause.GetType().FullName,
                    Message = cause.Message,
                    Frames = GenerateModelStackFrames(new StackTrace(cause, true))
                };
                if (topException == null)
                {
                    topException = exception;
                }
                else
                {
                    parentException.InnerExceptions = new List<ModelException> { exception };
                }
                parentException = exception;
            }
            return topException;
        }

        private static IList<ModelStackFrame> GenerateModelStackFrames(StackTrace stackTrace)
        {
            var modelFrames = new List<ModelStackFrame>();
            var frames = stackTrace.GetFrames();
            if (frames != null)
            {
                modelFrames.AddRange(frames.Select(frame => new ModelStackFrame
                {
                    ClassName = frame.GetMethod()?.DeclaringType?.ToString(),
                    MethodName = frame.GetMethod()?.Name,
                    FileName = frame.GetFileName(),
                    LineNumber = frame.GetFileLineNumber() != 0 ? new Java.Lang.Integer(frame.GetFileLineNumber()) : null
                }).Where(modelFrame => !modelFrame.Equals(EmptyModelFrame)));
            }
            return modelFrames;
        }

        private class CrashListener : AbstractCrashesListener
        {
            public override void OnCrashCaptured(ManagedErrorLog errorLog)
            {
                base.OnCrashCaptured(errorLog);
                _errorLog = errorLog;
                JoinExceptionAndLog();
            }
        }
    }
}