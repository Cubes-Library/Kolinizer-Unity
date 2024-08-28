using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kub.Util
{
    public static class Log
    {
        public enum LogLevel
        {
            Debug,
            Warn,
            Error,
            Exception,
            None
        }
        public static LogLevel ConsoleLogLevel = LogLevel.Debug;
        public static LogLevel SentryLogLevel = LogLevel.Error;


        public static void D(string msg, Dictionary<string, string> kv = null)
        {
            if (ConsoleLogLevel <= LogLevel.Debug)
            {
                Debug.Log(msg);                
            }
            if (SentryLogLevel <= LogLevel.Debug)
            {
                // TODO: add Sentry
            }
        }
        public static void W(string msg, Dictionary<string, string> kv = null)
        {
            if (ConsoleLogLevel <= LogLevel.Warn)
            {
                Debug.LogWarning(msg);
            }
            if (SentryLogLevel <= LogLevel.Warn)
            {
                // TODO: add Sentry
            }
        }
        public static void E(string msg, Dictionary<string, string> kv = null)
        {
            if (ConsoleLogLevel <= LogLevel.Error)
            {
                Debug.LogError(msg);
            }
            if (SentryLogLevel <= LogLevel.Error)
            {
                // TODO: add Sentry
            }
        }

        public static void Ex(Exception ex, Dictionary<string, string> kv = null)
        {
            if (ConsoleLogLevel <= LogLevel.Exception)
            {
                Debug.LogException(ex);
            }
            if (SentryLogLevel <= LogLevel.Exception)
            {
                // TODO: add Sentry
            }
        }
    }
}