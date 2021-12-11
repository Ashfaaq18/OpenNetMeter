using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace WhereIsMyData.Models
{
    class TimerTest
    {
        public int Seconds { get; set; }
        private Timer aTimer;

        public TimerTest()
        {
            SetTimer();
        }

        public void SetTimer()
        {
            // Create a timer with a two second interval.
            aTimer = new Timer(2000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.AutoReset = true;
            aTimer.Enabled = true;
        }

        private void OnTimedEvent(Object source, ElapsedEventArgs e)
        {
            Seconds = e.SignalTime.Second;
        }
    }

}
