using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SettlersOnlineClient
{
    public partial class LoginWindow : ChildWindow
    {
        public LoginWindow ()
        {
            InitializeComponent();
            LoginButton.IsEnabled = false;
        }

        private void OKButton_Click (object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void EmailText_TextChanged (object sender, TextChangedEventArgs e)
        {
            EnableLoginButton();
        }

        private void PasswordText_PasswordChanged (object sender, RoutedEventArgs e)
        {
            EnableLoginButton();
        }

        private void EnableLoginButton ()
        {
            LoginButton.IsEnabled = (String.Empty != EmailText.Text.Trim()) && (String.Empty != PasswordText.Password.Trim());
        }
    }
}

