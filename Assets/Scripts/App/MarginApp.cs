using System;
using System.Threading.Tasks;
using Margin.App.Services;
using Margin.App.Services.Net;
using Margin.App.Web;
using Margin.App.Web.Screens;
using Margin.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Margin.App
{
    /// <summary>
    /// Entry point. Creates itself when any scene starts (nothing to set up by hand), builds the
    /// UIDocument, loads the services and hands the screen over to the web port (Margin.App.Web):
    /// App.Start() picks Welcome, Setup or Home exactly like preview/js/app.js.
    /// It also owns the sync loop: polling instead of realtime websockets (see docs/TECHNICAL.md).
    /// </summary>
    public class MarginApp : MonoBehaviour, IStateHost
    {
        public const string ObjectName = "MarginApp";

        /// <summary>The running app (the Web facades read the services from here).</summary>
        public static MarginApp Instance { get; private set; }

        public AppState State { get; private set; }
        public ContentRepository Content { get; private set; }
        public Catalog Catalog => Content.Catalog;
        public Services.Clock Clock { get; private set; }
        public NotificationService Notifications { get; private set; }
        /// <summary>The apps Focus mode can silence (ScreenTimeService.Apps, icons in Resources/AppIcons).</summary>
        public ScreenTimeService ScreenTime { get; private set; }
        public BackendConfig Backend => Content.Backend;
        public AccountService Account { get; private set; }
        public SocialService Social { get; private set; }
        /// <summary>Real Gandhi prices for the Book screen's "Buy it" section.</summary>
        public PriceService Prices { get; private set; }

        StateStore _store;
        VisualElement _appRoot;
        SupabaseClient _supabase;
        float _nextPoll, _nextSync;
        bool _polling;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<MarginApp>() != null) return;
            var go = new GameObject(ObjectName);
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = CreatePanelSettings();
            go.AddComponent<MarginApp>();
            DontDestroyOnLoad(go);
            go.SetActive(true);
        }

        void Awake()
        {
            Instance = this;
            gameObject.name = ObjectName;
            Application.targetFrameRate = 60;
            Fonts.DisableBrokenKerning(); // before any text is laid out (see the comment there)

            var doc = GetComponent<UIDocument>();
            if (doc.panelSettings == null) doc.panelSettings = CreatePanelSettings();

            _store = new StateStore();
            State = _store.Load();
            Content = new ContentRepository();
            Clock = new Services.Clock();
            Notifications = new NotificationService();
            ScreenTime = new ScreenTimeService();

            _supabase = new SupabaseClient(Content.Backend);
            Account = new AccountService(_supabase, this);
            Social = new SocialService(_supabase, Account);
            Prices = new PriceService(_supabase, Content.Backend);
            _supabase.SessionLost += OnSessionLost;
            Social.News += OnSocialNews;
            Social.Updated += Web.Social.Changed;               // friends' data changed: live screens rebuild
            Account.Changed += RefreshIfSafe;                   // the server's diary or profile arrived
            Account.SettingsChanged += Web.App.ApplySettings;   // theme, text size, reduce motion

            ReadInviteLink(Application.absoluteURL);
            Application.deepLinkActivated += OnDeepLink;
        }

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            // .app: tokens, fonts and the theme class live here (Resources/UI/app.uss, loaded by MarginTheme.tss).
            _appRoot = new VisualElement { name = "app" };
            _appRoot.AddToClassList("app");
            root.Add(_appRoot);
            root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea(root));

            Web.App.Mount(_appRoot);
            ApplySafeArea(root);
            Web.App.Start();
            if (Account.SignedIn) Web.App.Run(ResumeAccount());
        }

        void OnDestroy()
        {
            Application.deepLinkActivated -= OnDeepLink;
            if (Instance == this) Instance = null;
        }

        /// <summary>Opened with a saved session: catch up with the server in the background.</summary>
        async Task ResumeAccount()
        {
            await Account.AdoptAccount();
            if (!Account.SignedIn) return;
            Web.App.ApplySettings();
            // The server already had a finished setup for this account (e.g. from the web).
            if (State.profile.onboardingComplete && Nav.Current is SetupScreen) Web.App.Start();
            await Social.Refresh();
            RefreshIfSafe();
        }

        static PanelSettings CreatePanelSettings()
        {
            var ps = Resources.Load<PanelSettings>("UI/MarginPanelSettings");
            if (ps != null) return ps;
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/MarginTheme");
            // 1 USS px = 1 CSS px of the web app at 390 points wide (iPhone 14/15): scale with the width.
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(390, 844);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0f;
            ps.clearColor = true;
            ps.colorClearValue = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
            return ps;
        }

        Rect _lastSafe;
        Vector2 _lastSize;

        /// <summary>Screen.safeArea (pixels) to panel units, for the notch and the home indicator.</summary>
        void ApplySafeArea(VisualElement root)
        {
            var safe = Screen.safeArea;
            var size = new Vector2(Screen.width, Screen.height);
            if ((safe == _lastSafe && size == _lastSize) || Screen.width == 0 || root.layout.width <= 0 || float.IsNaN(root.layout.width)) return;
            _lastSafe = safe;
            _lastSize = size;
            float k = root.layout.width / Screen.width; // pixels -> panel units
            _appRoot.style.paddingLeft = safe.x * k;
            _appRoot.style.paddingRight = (Screen.width - safe.xMax) * k;
            if (Safe.Update((Screen.height - safe.yMax) * k, safe.y * k)) Nav.Refresh();
        }

        // ---------- State ----------

        /// <summary>Saves the diary on the phone, then queues it (and any new activity) for the account.</summary>
        public void Save()
        {
            SaveLocally();
            Account.OnStateSaved();
            _nextSync = Mathf.Min(_nextSync, Time.unscaledTime + 1f); // send soon, in one batch (the web waits 1 s)
        }

        void SaveLocally()
        {
            try { _store.Save(State); }
            catch (Exception e)
            {
                Debug.LogError($"[Margin] Could not save: {e.Message}");
                Components.Toast("We couldn't save. Check your phone's storage.");
            }
        }

        // IStateHost: the account swaps in the server's diary (it is already synced, so it's not a change).
        void IStateHost.ReplaceState(AppState state)
        {
            State = state ?? new AppState();
            SaveLocally();
        }

        DateTime IStateHost.Today => Clock.Today;

        /// <summary>
        /// After the account was deleted on the server: removes the diary file and goes back to Welcome.
        /// (AccountService already forgot the session, the cached profile and the sync queue.)
        /// </summary>
        public void ForgetDevice()
        {
            _store.Delete();
            State = new AppState();
            Clock.ResetSimulation();
            Web.App.Start();
        }

        // ---------- Account events ----------

        void OnSessionLost()
        {
            Account.ForgetLocalAccount();
            Social.Reset();
            Web.App.Start();
            Components.Toast("Your session ended. Log in again to keep syncing.");
        }

        void OnSocialNews(System.Collections.Generic.List<SocialNews> news)
        {
            foreach (var n in news)
            {
                var name = AccountRules.DisplayName(n.Person);
                Components.Toast(n.NewRequest ? Copy.WantsToBeFriend(name) : Copy.AcceptedYou(name));
            }
        }

        /// <summary>Rebuilds the current screen if it shows server data and it is safe to do so (no sheet open).</summary>
        void RefreshIfSafe()
        {
            var c = Nav.Current;
            if (c != null && (c.Live || c.CanReplaceState) && Sheet.Cur == null) Nav.Refresh();
        }

        // ---------- Invite links (margin://...?add=username or the web URL) ----------

        void OnDeepLink(string url)
        {
            ReadInviteLink(url);
            if (Account.SignedIn && State.profile.onboardingComplete && !string.IsNullOrEmpty(Web.App.PendingAdd))
            {
                var add = Web.App.PendingAdd;
                Web.App.PendingAdd = null;
                if (add != Account.Me?.username) Nav.Push(new UserScreen(add));
            }
        }

        static void ReadInviteLink(string url)
        {
            if (!string.IsNullOrEmpty(url) && InviteLink.TryParse(url, out var username)) Web.App.PendingAdd = username;
        }

        // ---------- Sync loop (polling instead of realtime websockets) ----------

        void Update()
        {
            if (Account == null || !Account.SignedIn) return;
            float now = Time.unscaledTime;
            if (now >= _nextSync)
            {
                _nextSync = now + 15f; // retry unsent changes every 15 s (Save() brings it forward)
                if (Account.HasUnsentChanges) Web.App.Run(Account.Sync());
            }
            var current = Nav.Current;
            if (now >= _nextPoll && !_polling && current != null && current.Live)
            {
                _nextPoll = now + Mathf.Max(5, Backend.pollSeconds);
                Web.App.Run(Poll(false));
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && Account != null && Account.SignedIn) Web.App.Run(Poll(true));
        }

        async Task Poll(bool includeDiary)
        {
            if (_polling) return;
            _polling = true;
            try
            {
                var c = Nav.Current;
                bool replaced = includeDiary && c != null && c.CanReplaceState && await Account.PullIfIdle();
                await Social.Refresh(); // raises Updated -> Social.Changed() when something is new
                if (replaced) RefreshIfSafe();
            }
            finally { _polling = false; }
        }

        public void RescheduleReminders()
        {
            Notifications.Reschedule(State, Content, Clock.Now);
            Save();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && State != null && State.profile.onboardingComplete) RescheduleReminders();
            if (paused && Account != null && Account.SignedIn) Web.App.Run(Account.Sync()); // best effort before iOS suspends us
        }

        void OnApplicationQuit()
        {
            if (Account != null && Account.SignedIn) Web.App.Run(Account.Sync());
        }

        // ---------- Shortcuts ----------

        public Book CurrentBook => Catalog.FindBook(State.profile.currentBookId ?? "");
        public MomentumSnapshot Momentum => HabitEngine.Compute(State, Clock.Today);
    }
}
