using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SettlersOnlineClient
{
    public partial class App : Application
    {
        private Base.ILogger m_logger = new Base.NLogger();

        public App()
        {
            this.Startup += this.Application_Startup;
            this.Exit += this.Application_Exit;
            this.UnhandledException += this.Application_UnhandledException;

            InitializeComponent();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            this.RootVisual = new MainPage();

            // The first thing we do is show the login dialog.
            LoginWindow loginWindow = new LoginWindow();
            loginWindow.Closed += new EventHandler(OnLoginClosed);
            loginWindow.Show();
        }

        private void OnLoginClosed (object sender, EventArgs e)
        {
            LoginWindow loginWindow = (LoginWindow) sender;
            string name = loginWindow.NameText.Text;
            string password = loginWindow.PasswordText.Password;

            // Determine the DNS end point we need to connect to.
            System.Windows.Application application = System.Windows.Application.Current;
            Uri uri = application.Host.Source;
            DnsEndPoint endPoint = new DnsEndPoint(uri.Host, 4530, System.Net.Sockets.AddressFamily.InterNetwork);

            // Time to fire up the network thread and Login script.
            Network.ClientThread client = new Network.ClientThread(m_logger);
            Network.ConnectThread connect = new Network.ConnectThread(m_logger, client, endPoint);
            Login login = new Login(m_logger, client, name, password);
            
            // TODO: Create the connect window.

            client.Run();
            connect.Run();
        }

        private void Application_Exit(object sender, EventArgs e)
        {

        }

        private void Application_UnhandledException(object sender, ApplicationUnhandledExceptionEventArgs e)
        {
            // If the app is running outside of the debugger then report the exception using
            // the browser's exception mechanism. On IE this will display it a yellow alert 
            // icon in the status bar and Firefox will display a script error.
            if (!System.Diagnostics.Debugger.IsAttached)
            {

                // NOTE: This will allow the application to continue running after an exception has been thrown
                // but not handled. 
                // For production applications this error handling should be replaced with something that will 
                // report the error to the website and stop the application.
                e.Handled = true;
                Deployment.Current.Dispatcher.BeginInvoke(delegate { ReportErrorToDOM(e); });
            }
        }

        private void ReportErrorToDOM(ApplicationUnhandledExceptionEventArgs e)
        {
            try
            {
                string errorMsg = e.ExceptionObject.Message + e.ExceptionObject.StackTrace;
                errorMsg = errorMsg.Replace('"', '\'').Replace("\r\n", @"\n");

                System.Windows.Browser.HtmlPage.Window.Eval("throw new Error(\"Unhandled Error in Silverlight Application " + errorMsg + "\");");
            }
            catch (Exception)
            {
            }
        }
    }
}
