using System;
using System.Collections;
using System.Collections.Generic;
using Margin.Core;
using UnityEngine;
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Margin.App.Services
{
    /// <summary>
    /// Local reminders with book quotes. The policy (how many and when) is decided by
    /// <see cref="ReminderPlanner"/>; this class only schedules them with the OS.
    /// </summary>
    public class NotificationService
    {
        public bool IsSupported =>
#if UNITY_IOS && !UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>Asks the system for permission. In the editor it just simulates a yes.</summary>
        public IEnumerator RequestPermission(Action<bool> done)
        {
#if UNITY_IOS && !UNITY_EDITOR
            using (var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false))
            {
                while (!req.IsFinished) yield return null;
                done?.Invoke(req.Granted);
            }
#else
            yield return null;
            done?.Invoke(true);
#endif
        }

        /// <summary>Reschedules the next 7 days. Called when the app closes/pauses and after each check-in.</summary>
        public List<DateTime> Reschedule(AppState state, ContentRepository content, DateTime now)
        {
            var plan = ReminderPlanner.PlanNext7Days(state, now);
#if UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            ScheduleFocusEndWithOs(); // RemoveAll also dropped a running Focus session's alert
            for (int i = 0; i < plan.Count; i++)
            {
                var at = plan[i];
                var quote = content.QuoteOfTheDay(at);
                iOSNotificationCenter.ScheduleNotification(new iOSNotification
                {
                    Identifier = $"margin-{at:yyyyMMddHHmm}",
                    Title = Copy.ReminderTitle(state.profile),
                    Body = quote != null ? $"“{quote.text}” — {quote.author}" : Copy.ReminderFallback,
                    ShowInForeground = false,
                    Trigger = new iOSNotificationCalendarTrigger
                    {
                        Year = at.Year, Month = at.Month, Day = at.Day, Hour = at.Hour, Minute = at.Minute, Repeats = false
                    }
                });
            }
#else
            foreach (var at in plan) Debug.Log($"[Margin] Simulated reminder: {at:yyyy-MM-dd HH:mm}");
#endif
            // Saved to measure ignored reminders (adaptive reminder budget).
            foreach (var at in plan)
                if (!state.remindersScheduled.Exists(d => d.Date == at.Date)) state.remindersScheduled.Add(at);
            state.remindersScheduled.RemoveAll(d => d < now.AddDays(-60));
            return plan;
        }

        // ---------- Focus mode: the end of the session ----------

        const string FocusId = "margin-focus-end";
        DateTime? _focusEndUtc;
        string _focusTitle, _focusBody;

        /// <summary>
        /// A local notification when a Focus session ends (the phone is face down, the app may be in the
        /// background or locked). Replaces any earlier one; CancelFocusEnd removes it if the session ends early.
        /// </summary>
        public void ScheduleFocusEnd(DateTime endUtc, string title, string body)
        {
            _focusEndUtc = endUtc;
            _focusTitle = title;
            _focusBody = body;
#if UNITY_IOS && !UNITY_EDITOR
            ScheduleFocusEndWithOs();
#else
            Debug.Log($"[Margin] Simulated Focus alert at {endUtc.ToLocalTime():HH:mm:ss}: {_focusTitle}. {_focusBody}");
#endif
        }

        public void CancelFocusEnd()
        {
            _focusEndUtc = null;
#if UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveScheduledNotification(FocusId);
            iOSNotificationCenter.RemoveDeliveredNotification(FocusId);
#endif
        }

        void ScheduleFocusEndWithOs()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_focusEndUtc == null) return;
            var left = _focusEndUtc.Value - DateTime.UtcNow;
            if (left.TotalSeconds < 1) return;
            iOSNotificationCenter.RemoveScheduledNotification(FocusId);
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = FocusId,
                Title = _focusTitle,
                Body = _focusBody,
                // In the app the screen shows its own banner and vibrates; the system alert is for when it's closed.
                ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = left, Repeats = false },
            });
#endif
        }
    }
}
