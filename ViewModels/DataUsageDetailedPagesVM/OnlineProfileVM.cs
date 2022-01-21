﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhereIsMyData.Models;

namespace WhereIsMyData.ViewModels.DataUsageDetailedPagesVM
{
    public class OnlineProfileVM
    {
        public ObservableConcurrentDictionary<string, MyProcess> MyProcesses { get; set; }
        public OnlineProfileVM()
        {
            MyProcesses = new ObservableConcurrentDictionary<string, MyProcess>();
        }
    }
}