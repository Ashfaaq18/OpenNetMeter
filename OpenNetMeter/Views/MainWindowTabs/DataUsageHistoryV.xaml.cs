﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenNetMeter.Views
{
    /// <summary>
    /// Interaction logic for DataUsageHistoryV.xaml
    /// </summary>
    public partial class DataUsageHistoryV : UserControl
    {
        public DataUsageHistoryV()
        {
            InitializeComponent();
        }

        private void AllAppsData_LayoutUpdated(object sender, EventArgs e)
        {
            Total.Width = AllAppsData.Columns[0].ActualWidth;
            TotalDataRecv.Width = AllAppsData.Columns[1].ActualWidth;
            TotalDataSent.Width = AllAppsData.Columns[2].ActualWidth;
        }
    }
}
