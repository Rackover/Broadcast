using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

// LouveSystems Logger v1
namespace Broadcast.Shared
{
    public class Logger
    {
        public enum LEVEL { TRACE, DEBUG, WARNING, INFO, ERROR };
        readonly string logFilePath = @"logs/{0}{1}.log";

        LEVEL level;
        CultureInfo culture = new CultureInfo("fr-FR");
        Dictionary<LEVEL, ConsoleColor> colors = new Dictionary<LEVEL, ConsoleColor>()
        {
            {LEVEL.TRACE, ConsoleColor.Magenta },
            {LEVEL.DEBUG, ConsoleColor.Gray },
            {LEVEL.INFO, ConsoleColor.White },
            {LEVEL.WARNING, ConsoleColor.Yellow },
            {LEVEL.ERROR, ConsoleColor.Red }
        };
        int flushEvery = 1000;

        bool outputToFile = false;
        bool outputToConsole = true;

        FileStream logFileStream = null;
        string programName;
        Timer flushTimer;
        Action<object> logFunction = (Action<object>)Console.WriteLine;

        public Logger(string programName = null, bool outputToFile = false, bool outputToConsole = true)
        {
            if (programName == null) programName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);

            Initialize(programName, outputToFile, outputToConsole);
        }

        void Initialize(string programName, bool outputToFile = false, bool outputToConsole = true)
        {
            this.programName = programName;
            this.outputToFile = outputToFile;
            this.outputToConsole = outputToConsole;

            if (outputToFile) {
                var filePath = string.Format(logFilePath, this.programName, "");
                Directory.CreateDirectory(
                Path.GetDirectoryName(
                        filePath
                    )
                );
                if (flushTimer != null) flushTimer.Dispose();
                if (logFileStream != null) logFileStream.Dispose();
                logFileStream = null;

                int i = 0;
                while (logFileStream == null) {
                    try {
                        filePath = string.Format(logFilePath, this.programName, i == 0 ? "" : i.ToString());
                        if (File.Exists(filePath)) File.Delete(filePath);

                        logFileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    }
                    catch (IOException) {
                        // File is locked - increment and retry
                        i++;
                    }
                }

                flushTimer = new Timer(
                    e => {
                        logFileStream.Flush();
                    },
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(flushEvery));
            }
        }

        public void SetLevel(LEVEL level)
        {
            this.level = level;
        }

        public void SetConsoleFunction(Action<object> function)
        {
            this.logFunction = function;
        }

        public void Trace(string format, params object[] msgs) { LogMessage(LEVEL.TRACE, format, msgs); }
        public void Debug(string format, params object[] msgs) { LogMessage(LEVEL.DEBUG, format, msgs); }
        public void Info(string format, params object[] msgs) { LogMessage(LEVEL.INFO, format, msgs); }
        public void Warn(string format, params object[] msgs) { LogMessage(LEVEL.WARNING, format, msgs); }
        public void Error(string format, params object[] msgs) { LogMessage(LEVEL.ERROR, format, msgs); }
        public void Fatal(Exception e)
        {
            LogMessage(LEVEL.ERROR, "================== FATAL ==================");
            LogMessage(LEVEL.ERROR, e.ToString());
            Console.ReadKey();
            Environment.Exit(1);
        }

        void LogMessage(LEVEL msgLevel, string format, params object[] msgs)
        {
            if (msgLevel < level) {
                return;
            }

            string caller = programName;
            
            // Debug line formatting
            string line = "{0} [{1}] [{2}]: {3}";
            line = string.Format(line, DateTime.Now.ToString(culture.DateTimeFormat.LongTimePattern), msgLevel.ToString(), caller, string.Format(format, msgs));

            if (outputToConsole) {
                Console.ForegroundColor = colors[msgLevel];
                logFunction(line);
            }

            if (outputToFile) {
                using (StreamWriter sw = new StreamWriter(logFileStream, Encoding.UTF8, 1024, leaveOpen: true)) {
                    sw.WriteLine(line);
                }
            }
        }
    }
}