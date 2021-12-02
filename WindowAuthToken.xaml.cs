using LibKaseya;
using System.Management;
using System.Windows;

namespace KLCEx {
    /// <summary>
    /// Interaction logic for WindowAuthToken.xaml
    /// </summary>
    public partial class WindowAuthToken : Window
    {
        public WindowAuthToken()
        {
            InitializeComponent();
        }

        public static string GetInput(string starter, Window owner)
        {
            WindowAuthToken form = new WindowAuthToken();
            form.Owner = owner;
            form.txtAuthToken.Password = starter;

            bool? result = form.ShowDialog();
            if (result == true)
            {
                string token = form.txtAuthToken.Password.Trim();
                //Save the token until the computer is logged out
                Kaseya.LoadToken(token);
                KaseyaAuth.SetCredentials(token);
                return token;
            }
            else
                return starter;
        }

        public string ResponseText
        {
            get { return txtAuthToken.Password; }
            set { txtAuthToken.Password = value; }
        }

        private void OKButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void btnAuthCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(txtAuthToken.Password);
        }

        private void btnAuthGetFromKLC_Click(object sender, RoutedEventArgs e)
        {
            ManagementClass mngmtClass = new ManagementClass("Win32_Process");
            foreach (ManagementObject o in mngmtClass.GetInstances())
            {
                if (o["Name"].Equals("KaseyaLiveConnect.exe"))
                {
                    string find = "liveconnect:///";
                    string commandline = (string)o["CommandLine"];
                    int pos = commandline.IndexOf(find);
                    if (pos > 0)
                    {
                        string base64 = commandline.Substring(pos + find.Length);
                        KLCCommand command = KLCCommand.NewFromBase64(base64);
                        txtAuthToken.Password = command.payload.auth.Token;
                        return;
                    }
                }
            }
        }
    }
}
