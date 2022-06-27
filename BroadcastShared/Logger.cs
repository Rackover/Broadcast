using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

// LouveSystems Logger v1
namespace Broadcast.Shared
{
    public class Logger
    {
        public enum LEVEL { TRACE, DEBUG, WARNING, INFO, ERROR };
        readonly string logFilePath = @"logs/{0}{1}.log";

        readonly LEVEL level = LEVEL.TRACE;
        CultureInfo culture = new CultureInfo("fr-FR");
        Dictionary<LEVEL, ConsoleColor> colors = new Dictionary<LEVEL, ConsoleColor>()
        {
            {LEVEL.TRACE, ConsoleColor.Gray },
            {LEVEL.DEBUG, ConsoleColor.White },
            {LEVEL.INFO, ConsoleColor.Green },
            {LEVEL.WARNING, ConsoleColor.Yellow },
            {LEVEL.ERROR, ConsoleColor.Red }
        };
        int flushEvery = 1000;

        bool outputToFile = false;
        bool outputToConsole = true;

        StreamWriter logWriter;
        string programName;
        Timer flushTimer;
        Action<object> logFunction = (Action<object>)Console.WriteLine;
        
        readonly object mutex = new object();

        public Logger(string programName = null, bool outputToFile = false, bool outputToConsole = true, bool addDateSuffix = false)
        {
            if (programName == null) programName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);

            Initialize(programName, outputToFile, outputToConsole, addDateSuffix);
        }

        void Initialize(string programName, bool outputToFile = false, bool outputToConsole = true, bool addDateSuffix = false)
        {
            this.programName = programName;
            this.outputToFile = outputToFile;
            this.outputToConsole = outputToConsole;

            if (outputToFile) {
                var filePath = string.Format(logFilePath, this.programName, addDateSuffix ? DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss") : string.Empty);
                Directory.CreateDirectory(
                Path.GetDirectoryName(
                        filePath
                    )
                );
                flushTimer?.Dispose();
                logWriter?.Dispose();

                FileStream fs = null;
                string originalFilePath = filePath;

                for (int i = 0; i < 10; i++) {
                    try {
                        fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    }
                    catch (IOException) {
                        filePath = $"{originalFilePath}.{i+1}";
                    }
                }
                 
                logWriter = new StreamWriter(fs, Encoding.UTF8, 1024);

                flushTimer = new Timer(
                    e =>
                    {
                        logWriter.Flush();
                    },
                    null,
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(flushEvery));
            }
        }



        public void Trace(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.TRACE, msgs, filePath); }
        public void Debug(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.DEBUG, msgs, filePath); }
        public void Info(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.INFO, msgs, filePath); }
        public void Warn(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.WARNING, msgs, filePath); }
        public void Error(object msgs, [CallerFilePath] string filePath = "") { LogMessage(LEVEL.ERROR, msgs, filePath); }

        void LogMessage(LEVEL msgLevel, object msgs, string filePath = "")
        {
            lock (mutex) {
                if (msgLevel < level) {
                    return;
                }

                string caller = programName;

                // Debug line formatting
                string line = "{0} [{1}] [{2}]:{3}";
                line = string.Format(line, DateTime.Now.ToString("G", culture), msgLevel.ToString(), filePath, string.Join(" ", msgs));
                if (outputToConsole) {
                    Console.ForegroundColor = colors[msgLevel];
                    logFunction(line);
                }

                if (outputToFile) {
                    logWriter?.WriteLine(line);
                }
            }
        }
    }
}