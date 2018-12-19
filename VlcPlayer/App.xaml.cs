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
    public partial class App : Application
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var allocConsole = logger?.Factory?.Configuration?.Variables?.FirstOrDefault(v=>v.Key == "AllocConsole");
                if (allocConsole?.Value?.Text == "true")
                {
                    VlcContracts.NativeMethods.AllocConsole();
                }

                logger.Info("========== START ============");

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

                            if (a.IsTerminating)
                            {
                                Environment.FailFast(ex.Message, ex);
                            }
                            else
                            {
                                playback?.Close();
                            }

                            //int errorCode = ProcessException(ex);
                            // Environment.Exit(errorCode);

                        }
                    }

                };

                var application = new App();
                application.InitializeComponent();

                CommandLineOptions = ParseCommandLine(args);

                var parentId = CommandLineOptions.ParentId;
                if (parentId > 0)
                {
                    parentProcess = Process.GetProcessById(parentId);
                    if (parentProcess != null)
                    {
                        parentProcess.EnableRaisingEvents = true;
                        parentProcess.Exited +=(o,a)=> 
                        {
                            logger.Warn("Parent process exited...");
                            try
                            {
                                playback?.Close();
                            }
                            catch(Exception ex) { }
                            Environment.Exit(-1);

                            //if (playback != null)
                            //{
                            //    playback.Close();
                            //}
                            //else
                            //{
                            //    Environment.Exit(-1);
                            //}
                        };
                    }
                }

                playback = new PlaybackHost(application);

                if (parentProcess == null)
                {
                    VideoWindow window = new VideoWindow
                    {
                        DataContext = playback,
                    };

                    window.Show();
                }

                playback.Setup();
                playback.Closed += (obj) => 
                {
                    playback.Dispose();
                    Environment.Exit(0);
                };

                application.Run();

                //System.Windows.Threading.Dispatcher.Run();

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
            var options = new CommandLineOptions();
            if (args != null)
            {
                logger.Info("Command Line String: " + string.Join(" ", args));
                bool parseResult = Parser.Default.ParseArguments(args, options);
                if(!parseResult)
                {
                    //...
                }
            }
            return options;
        }

        private  int ProcessException(Exception ex)
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
        public static readonly System.IO.DirectoryInfo VlcLibDirectory = new System.IO.DirectoryInfo(System.IO.Path.Combine(App.CurrentDirectory, "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));

        public static CommandLineOptions CommandLineOptions { get; private set; }

        internal static Process parentProcess = null;
        public static IntPtr ParentWindowHandle
        {
            get
            {
                IntPtr handle = IntPtr.Zero;
                if (parentProcess != null)
                {
                    handle = parentProcess.MainWindowHandle;
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
    }

}
