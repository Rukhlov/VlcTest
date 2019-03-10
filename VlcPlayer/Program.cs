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
        public static void Main(string[] args)
        {
            try
            {
                logger = LogManager.GetCurrentClassLogger();
                var allocConsole = logger?.Factory?.Configuration?.Variables?.FirstOrDefault(v => v.Key == "AllocConsole");
                if (allocConsole?.Value?.Text == "true")
                {
                    VlcContracts.NativeMethods.AllocConsole();
                }

                logger.Info("========== START ============");

                bool dispatching = false;
                PlaybackHost playback = null;

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

                            //if (a.IsTerminating)
                            //{
                            //    Environment.FailFast(ex.Message, ex);
                            //}
                            //else
                            //{
                            //    playback?.Close();
                            //}

                            int code = ProcessException(ex);
                            Environment.Exit(code);

                        }
                    }

                };

                CommandLineOptions = ParseCommandLine(args);
                if(CommandLineOptions == null)
                {
                    CommandLineOptions = new CommandLineOptions();
                }

                var parentId = CommandLineOptions.ParentId;
                if (parentId > 0)
                {
                    ParentProcess = Process.GetProcessById(parentId);
                    if (ParentProcess != null)
                    {
                        ParentProcess.EnableRaisingEvents = true;
                        ParentProcess.Exited += (o, a) =>
                        {
                            logger.Warn("Parent process exited...");
                            try
                            {
                                if (dispatching)
                                {
                                    playback.Close();
                                }
                                else
                                {
                                    Environment.Exit(-1);
                                }                             
                            }
                            catch (Exception ex)
                            {
                                logger.Error(ex);
                            }
                        };
                    }
                }

                playback = new PlaybackHost();
                playback.Closed += (obj, ex) =>
                {
                    logger.Debug("playback.Closed(...)");

                    if (ex != null)
                    {

                    }

                    System.Windows.Threading.Dispatcher dispatcher = null;
                    if (obj != null)
                    {
                        dispatcher = obj as System.Windows.Threading.Dispatcher;
                        if (dispatcher != null)
                        {
                            dispatcher.InvokeShutdown();
                            //dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Normal);
                        }
                    }

                    if(dispatcher == null)
                    {
                        Environment.Exit(-2);
                    }
                };

                playback.Setup();
               
                dispatching = true;
                System.Windows.Threading.Dispatcher.Run();

            }
            catch(Exception ex)
            {
                logger.Fatal(ex);
            }
            finally
            {
                VlcContracts.NativeMethods.FreeConsole();
                logger.Info("========== THE END ============");
            }
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


