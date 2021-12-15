using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhereIsMyData.ViewModels
{
    public class DataUsageDetailedVM
    {
        private Dictionary<string, ulong> processNames;

        public void EditProcessInfo(string name, ulong data)
        {
            try
            {
                processNames.Add(name, data);
            }
            catch
            {
                processNames[name] = processNames[name] + data;
            }
        }

        public DataUsageDetailedVM()
        {
            processNames = new Dictionary<string, ulong>();
        }
    }
}
