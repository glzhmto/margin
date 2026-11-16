using System;
using UnityEngine;

namespace Margin.App.Services
{
    /// <summary>
    /// The app clock. In Demo mode it can move days forward to show during the
    /// presentation how reading momentum grows without waiting a week.
    /// </summary>
    public class Clock
    {
        const string OffsetKey = "margin.demo.dayOffset";

        public int DayOffset
        {
            get => PlayerPrefs.GetInt(OffsetKey, 0);
            private set { PlayerPrefs.SetInt(OffsetKey, value); PlayerPrefs.Save(); }
        }

        public DateTime Now => DateTime.Now.AddDays(DayOffset);
        public DateTime Today => Now.Date;
        public bool IsSimulated => DayOffset != 0;

        public void AdvanceDay() => DayOffset += 1;
        public void ResetSimulation() => DayOffset = 0;
    }
}
