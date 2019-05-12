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

                //var allocConsole = logger?.Factory?.Configuration?.Variables?.FirstOrDefault(v => v.Key == "AllocConsole");
                //if (allocConsole?.Value?.Text == "true")
                //{
                //    VlcContracts.NativeMethods.AllocConsole();
                //}

                logger.Info("========== START ============");


                PlaybackHost playbackHost = new PlaybackHost();

               // VlcPlayback playback = new VlcPlayback();

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

                            Quit(playbackHost, code);

                        }
                    }

                };


                Session.Init(args);

                var parentProc = Session.ParentProcess;
                if (parentProc != null && !parentProc.HasExited)
                {
                  
                    parentProc.EnableRaisingEvents = true;
                    parentProc.Exited += (o, a) =>
                    {
                        logger.Warn("Parent process exited...");
                        Quit(playbackHost, -1);

                    };
                }
                else
                {
                    //return -100502;
                }
                

                logger.Info("============ RUN ===============");
                playbackHost.Run();

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

                if (Session.ParentWindowHandle == IntPtr.Zero)
                {
                    string message = ex.Message;
                    string title = "Error!";
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                }


                //TODO
            }

            return code;
        }

        public static void Quit(PlaybackHost host, int code)
        {
            try
            {
                if (host != null)
                {
                    host.Quit();
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

    }


    public class Session
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private Session()
        { }

        public static readonly string CurrentDirectory = new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).DirectoryName;
        public static readonly System.IO.DirectoryInfo VlcLibDirectory = new System.IO.DirectoryInfo(System.IO.Path.Combine(CurrentDirectory, "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));


        private static Session config = null;
        private static readonly object syncRoot = new object();

        public static Session Config
        {
            get
            {
                lock (syncRoot)
                {
                    if (config == null)
                    {
                        config = new Session();
                    }

                    return config;
                }
            }
        }

        public Guid ExchangeId { get; set; }
        public IntPtr VideoOutHandle { get; set; }

        public CommandLineOptions Options { get; private set; } = new CommandLineOptions();
        //public Defaults Defaults { get; private set; } = new Defaults();

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

        public static void Init(string[] args)
        {
            if (args != null)
            {
                logger.Info("Command Line String: " + string.Join(" ", args));

                var options = Config.Options;
                bool res = Parser.Default.ParseArguments(args, options);
                if (!res)
                {
                    // options = null;
                }

                var parentId = options.ParentId;
                if (parentId > 0)
                {
                    ParentProcess = Process.GetProcessById(parentId);
                }
            }
        }
    }

    //public class Defaults
    //{
    //    public readonly static int Width = 1920;
    //    public readonly static int Height = 1080;
    //    public System.Windows.Media.PixelFormat PixelFormat = System.Windows.Media.PixelFormats.Bgra32;
    //    public string VlcAudioOutput = "";
    //    public string VlcVideoOutput = "";
    //}

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


