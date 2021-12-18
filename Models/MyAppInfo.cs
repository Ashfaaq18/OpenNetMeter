using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereIsMyData.Models
{
    public class MyAppInfo
    {
        private string name;

        public string Name
        {
            get { return name; }
            set { name = value; }
        }


        private ulong dataRecv;
        public ulong DataRecv
        {
            get { return dataRecv; }
            set { dataRecv = value; }
        }

        public MyAppInfo(string name, ulong data)
        {
            Name = name;
            DataRecv = data;
        }
    }
}
