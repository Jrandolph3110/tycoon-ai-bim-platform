using System;
using System.Windows;

namespace TycoonRevitAddin.UI
{
    public partial class TestLayoutDialog : Window
    {
        public TestLayoutDialog()
        {
            InitializeComponent();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("🎉 Test button works! XAML is loading correctly!", 
                          "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
