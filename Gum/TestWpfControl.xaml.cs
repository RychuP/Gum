﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Gum
{
    /// <summary>
    /// Interaction logic for TestWpfControl.xaml
    /// </summary>
    public partial class TestWpfControl : UserControl
    {
        public object Instance
        {
            get { return DataGrid.Instance; }
            set { DataGrid.Instance = value; }
        }


        public TestWpfControl()
        {
            InitializeComponent();
        }
    }
}
