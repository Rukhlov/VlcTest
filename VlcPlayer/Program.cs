using CommandLine;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace VlcPlayer
{
    public class Program
    {
        private static Logger logger = null;//LogManager.GetCurrentClassLogger();

        [STAThread]
        public static int Main(string[] args)
        {
            int exitCode = 0;
            try
            {
                logger = LogManager.GetCurrentClassLogger();
                var allocConsole = logger?.Factory?.Configuration?.Variables?.FirstOrDefault(v => v.Key == "AllocConsole");
                if (allocConsole?.Value?.Text == "true")
                {
                    VlcContracts.NativeMethods.AllocConsole();
                }

                logger.Info("========== START ============");


                PlaybackHost playback = new PlaybackHost();

                AppDomain.CurrentDomain.UnhandledException += (o, a) =>
                {
                    logger.Debug("CurrentDomain_UnhandledException(...) " + a.IsTerminating);

                    var obj = a.ExceptionObject;
                    if (obj != null)
                    {
                        Exception ex = obj as Exception;
                        if (ex != null)
                        {
                            logger.Fatal(ex);

                            int code = ProcessException(ex);

                            Quit(playback, code);

                        }
                    }

                };

                
                CommandLineOptions = ParseCommandLine(args);
                if (CommandLineOptions == null)
                {
                    CommandLineOptions = new CommandLineOptions();
                }

                var parentId = CommandLineOptions.ParentId;
                if (parentId > 0)
                {                  
                    ParentProcess = Process.GetProcessById(parentId);
                    if (ParentProcess != null && !ParentProcess.HasExited)
                    {

                        ParentProcess.EnableRaisingEvents = true;
                        ParentProcess.Exited += (o, a) =>
                        {
                            logger.Warn("Parent process exited...");
                            Quit(playback, -1);

                        };
                    }
                    else
                    {
                        //return -100502;
                    }
                }

                PlaybackSession session = new PlaybackSession(CommandLineOptions);

                playback.Start(session);


                logger.Info("============ RUN ===============");
                System.Windows.Threading.Dispatcher.Run();

            }
            catch (Exception ex)
            {
                exitCode = -100500;
                logger.Fatal(ex);
            }
            finally
            {
                VlcContracts.NativeMethods.FreeConsole();
                logger.Info("========== THE END ============");
            }

            return exitCode;
        }

        private static CommandLineOptions ParseCommandLine(string[] args)
        {
            logger.Debug("ParseCommandLine(...)");
            CommandLineOptions options = null;
            if (args != null)
            {
                logger.Info("Command Line String: " + string.Join(" ", args));

                options = new CommandLineOptions();
                bool res = Parser.Default.ParseArguments(args, options);
                if (!res)
                {
                    // options = null;
                }
            }
            return options;
        }

        private static int ProcessException(Exception ex)
        {
            logger.Debug("ProcessException(...)");

            int code = -1;

            if (ex != null)
            {
                code = -2;

                if (ParentWindowHandle == IntPtr.Zero)
                {
                    string message = ex.Message;
                    string title = "Error!";
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                }


                //TODO
            }

            return code;
        }

        private static void Quit(PlaybackHost host, int code)
        {
            try
            {
                if (host != null)
                {
                    host.Close();
                }
                else
                {
                    Environment.Exit(code);
                }
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);

                Process.GetCurrentProcess().Kill();
            }
        }

        public static readonly string CurrentDirectory = new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
        public static readonly System.IO.DirectoryInfo VlcLibDirectory = new System.IO.DirectoryInfo(System.IO.Path.Combine(Program.CurrentDirectory, "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));

        public static CommandLineOptions CommandLineOptions { get; private set; }

        public static Process ParentProcess { get; private set; }

        public static IntPtr ParentWindowHandle
        {
            get
            {
                IntPtr handle = IntPtr.Zero;
                if (ParentProcess != null)
                {
                    handle = ParentProcess.MainWindowHandle;
                }
                return handle;
            }
        }
    }

    public class CommandLineOptions
    {
        [Option("channel")]
        public string ServerAddr { get; set; }

        [Option("media")]
        public string FileName { get; set; }

        [Option("parentid")]
        public int ParentId { get; set; }

        [Option("eventid")]
        public string SyncEventId { get; set; }

        [Option("vlcopts")]
        public string VlcOptions { get; set; }

        [Option("hwnd")]
        public int WindowHandle { get; set; }
    }
}


