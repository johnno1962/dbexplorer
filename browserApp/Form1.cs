//
// Shell form to host jSQL database explorer "DBExplorer".
//

using System;
using System.ComponentModel;
using System.Windows.Forms;

using System.IO;
using System.Net;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Threading;

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace browserApp
{
    public partial class Form1 : Form
    {
        [DllImport("loadjava.dll", 
        EntryPoint = "loadjava",
        ExactSpelling = false)]

        public static extern int loadjava([MarshalAs(UnmanagedType.LPWStr)] string dll,
                                [MarshalAs(UnmanagedType.LPStr)] string classpath,
                                [MarshalAs(UnmanagedType.LPStr)] string mainclass,
                                [MarshalAs(UnmanagedType.LPStr)] string port);

        string assemblyVersion;
        int serverPort = 21962;
        Process javaProcess;
        StreamWriter log;

        public Form1() {
            InitializeComponent();
        }

        string getenv(string name) {
            return Environment.GetEnvironmentVariable(name);
        }

        string geturl(string url) {
            return new StreamReader(WebRequest.Create(url)
                        .GetResponse().GetResponseStream()).ReadToEnd();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            webBrowser1.DocumentTitleChanged +=
                new EventHandler(webBrowser1_DocumentTitleChanged);
            this.Closing += new CancelEventHandler(Form1_Closing);

            string jsqlPort = getenv("JSQL_PORT");
            if (jsqlPort != null)
                serverPort = Int16.Parse(jsqlPort);

            Thread server = new Thread(new ThreadStart(startWebServer));
            server.IsBackground = true;
            server.Start();

            // fetch home URL until server process becomes available
            string vers, root = "http://127.0.0.1:" + serverPort,
                intro = root + "/jsqldocs/intro.html";

            while(true)
                try {
                    vers = geturl( intro );
                    break;
                }
                catch ( Exception ex ) {
                    Console.WriteLine( ex.Message );
                    Thread.Sleep(1000);
                }

            vers = vers.Substring(vers.IndexOf("Build: ")+8,4);
            if ("1.0.0."+vers != assemblyVersion)
                MessageBox.Show( "Jar build number: #" + vers + 
                    " != " + assemblyVersion + " assembly version." );

            this.Text = "DBExplorer Database Browser - Build: " + vers;
            webBrowser1.Navigate( root + "/jsqldocs/jsql.html" );
        }

        private void startWebServer()
        {
            string assemblyPrefix = "browserApp.Resources.", tmpDir = getenv("TEMP") + "\\jsql",
                versPath = tmpDir + "\\version.txt", javaHome = getenv("JAVA_HOME"),
                classPath = getenv("JSQL_CLASSPATH"), javaArgs = getenv("JSQL_ARGS");
            bool debug = getenv("JSQL_DEBUG") != null && true, newVersion = true;

            //Console.WriteLine(System.Guid.NewGuid().ToString());
            Assembly resourceAssembly = Assembly.GetExecutingAssembly();
            assemblyVersion = resourceAssembly.GetName().Version.ToString();

            if (classPath == null)
                classPath = ".";

            if (!Directory.Exists(tmpDir))
                Directory.CreateDirectory(tmpDir);
            else if (true)
                try {
                    StreamReader versFile = new StreamReader(versPath);
                    string vers = versFile.ReadLine();
                    versFile.Close();
                    if (vers == assemblyVersion)
                        newVersion = false;
                }
                catch (Exception ex) { }

            // copy app jars into tmp directory 
            // ... and construct java classpath
            foreach (string resourceName in
                resourceAssembly.GetManifestResourceNames())
            {
                String jarPath = tmpDir + "\\" + resourceName.Substring(assemblyPrefix.Length);
                if (newVersion || !File.Exists(jarPath) || debug)
                {
                    Stream objStream = resourceAssembly.GetManifestResourceStream(resourceName);
                   Byte[] resource = new Byte[objStream.Length];
                    objStream.Read(resource, 0, resource.Length);

                    FileStream fileStream = new FileStream(jarPath, FileMode.Create);
                    fileStream.Write(resource, 0, resource.Length);
                    fileStream.Close();
                }

                if (resourceName.Contains(".jar"))
                    classPath += ";" + jarPath;
            }

            if (getenv("CLASSPATH") != null && true)
                classPath += ";" + getenv("CLASSPATH");

            // prepare command to run up java VM
            string javaBinary = javaHome == null ?
                    "java.exe" : javaHome + "\\bin\\java.exe",
                javaOptions = (javaArgs == null ? "" :
                    javaArgs.Replace(" ", "\n") + "\n") +
                    "-Djava.class.path=" + classPath,
                dllpath = getenv("PATH") + ";" + tmpDir, 
                logfile = tmpDir + "\\jsql.log";

            javaArgs = (javaArgs != null ? javaArgs + " " : "") +
                        "-cp \"" + classPath + "\"" +
                        " org.jhttpd.WebServer " + -serverPort;

            StreamWriter versFile1 = new StreamWriter(versPath);
            versFile1.WriteLine(assemblyVersion);
            versFile1.Close();

            try
            {
                log = new StreamWriter(logfile);
                log.WriteLine("Command: " + javaBinary + " " + javaArgs);
                log.AutoFlush = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open log file " + logfile +
                    " for writing - only one instance of DBExplorer should be run at a time.");
            }

            Exception firstTry;
            if (!debug)
                try
                {
                    Environment.SetEnvironmentVariable("PATH", dllpath);

                    string jpath = @"HKEY_LOCAL_MACHINE\SOFTWARE\JavaSoft\Java Runtime Environment";
                    string jvers = (string)Registry.GetValue(jpath, "CurrentVersion", null);
                    string jhome = (string)Registry.GetValue(jpath + "\\" + jvers, "JavaHome", null);
                    Console.WriteLine("Registry JAVA_HOME: " + jhome);

                    string[] jvm_path = new string[] {
                        jhome,
                        getenv("ProgramFiles") + "\\Java\\jre6",
                        getenv("ProgramFiles(x86)") + "\\Java\\jre6",
                        getenv("ProgramFiles") + "\\Java\\jre7",
                        getenv("ProgramFiles(x86)") + "\\Java\\jre7",
                        getenv("ProgramFiles") + "\\Java\\jre8",
                        getenv("ProgramFiles(x86)") + "\\Java\\jre8",
                        (javaHome != null ? javaHome : "..") + "\\jre",
                        (javaHome != null ? javaHome : "..")
                    };

                    // try loading jvm into process using loadjava.dll
                    foreach (string jre_dir in jvm_path) {
                        int ret = loadjava(
                            jre_dir + "\\bin\\client\\jvm.dll",
                            javaOptions,
                            "org/jhttpd/WebServer",
                            "-" + serverPort);

                        // JVM has run and returned ok
                        if (ret == 0)
                            return;
                        Console.WriteLine("loadjava() returned: " + ret);
                    }
                }
                catch (Exception ex) {
                    MessageBox.Show("Could not load Web Server process: "+ex.Message);
                    firstTry = ex;
                }

            try {
                // otherwise falls back to web server as separate java process
                ProcessStartInfo processInfo = new ProcessStartInfo(javaBinary, javaArgs);
                processInfo.CreateNoWindow = !debug;
                processInfo.UseShellExecute = false;

                if (!debug) {
                    processInfo.RedirectStandardOutput = true;
                    processInfo.RedirectStandardError = true;
                }

                javaProcess = Process.Start(processInfo);

                if (!debug) {
                    new Thread(new ThreadStart(logStdout)).Start();
                    new Thread(new ThreadStart(logStderr)).Start();
                }
            }
            catch (Exception ex) {
                MessageBox.Show("Could not start Web Server process. Do you have Java installed?\n" +
                "Is your JAVA_HOME enironment variable set correctly?\n" + ex.Message);
            }
        }

        public void logStdout() {
            logReader(javaProcess.StandardOutput);
        }

        public void logStderr() {
            logReader(javaProcess.StandardError);
        }

        void logReader(StreamReader sr) {
            string line;
            while ((line = sr.ReadLine()) != null)
                if (log != null)
                    log.WriteLine(line);
        }

        private void webBrowser1_DocumentTitleChanged(object sender, EventArgs e) {
            if (webBrowser1.DocumentTitle.Contains(" - Query Cancelled"))
                webBrowser1.Stop();
            this.Text = webBrowser1.DocumentTitle;
        }

        private void webBrowser1_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e) {
        }

        private void Form1_Closing(object sender, CancelEventArgs e) {
            if (javaProcess != null)
                try {
                    javaProcess.Kill();
                }
                catch (Exception ex) {
                    MessageBox.Show("Could not kill java process - check task manager.");
                }
            //else
            //    geturl("http://127.0.0.1:" + serverPort + "/jsql/do.exit");
        }
    }
}
