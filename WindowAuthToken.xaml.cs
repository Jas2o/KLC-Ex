using LibKaseya;
using System;
using System.Collections.Generic;
using System.Windows;

namespace KLCEx {
    /// <summary>
    /// Interaction logic for WindowAuthToken.xaml
    /// </summary>
    public partial class WindowAuthToken : Window
    {
        public string ReturnAddress;
        public string ReturnToken;

        public WindowAuthToken()
        {
            InitializeComponent();

            foreach (KeyValuePair<string, KaseyaVSA> vsa in Kaseya.VSA)
            {
                cmbAddress.Items.Add(vsa.Key);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (cmbAddress.Items.Count > 0)
            {
                cmbAddress.SelectedIndex = 0;
                RefreshToken();
            }
        }

        private void OKButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //if (cmbAddress.SelectedIndex == -1)
            //return;

            ReturnAddress = cmbAddress.SelectedItem.ToString().Trim();
            ReturnToken = txtAuthToken.Password.Trim();

            if (ReturnAddress.Length > 0 && ReturnToken.Length > 0)
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void btnAuthCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(txtAuthToken.Password);
        }

        private void btnAuthGetFromKLC_Click(object sender, RoutedEventArgs e)
        {
            /* //Not .NET 6 compatible
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
            */
        }

        private void RefreshToken()
        {
            foreach (KeyValuePair<string, KaseyaVSA> vsa in Kaseya.VSA)
            {
                if (vsa.Key == cmbAddress.Text)
                {
                    txtAuthToken.Password = vsa.Value.Token;
                    return;
                }
            }

            txtAuthToken.Password = "";
        }

        private void cmbAddress_DropDownClosed(object sender, EventArgs e)
        {
            RefreshToken();
        }

        private void cmbAddress_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            RefreshToken();
        }
    }
}
