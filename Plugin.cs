using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using TMPro;
using UnityEngine;

namespace ShowMeThePlates
{
    // HUD de durabilidad de las placas del chaleco (solo placas, sin insertos ni casco).
    // Se muestra manteniendo pulsada la tecla (R por defecto) o siempre, segun config (F12).
    [BepInPlugin("com.showmetheplates", "ShowMeThePlates", "1.0.0")]
    public partial class Plugin : BaseUnityPlugin
    {
        public enum ShowMode { AutoHide, AlwaysOn }
        public enum WarnStyle { Notification, Panel, Both }

        class Row { public string Id, Label, Txt, Full; public float Cur, Max; public int Kind, Zone; public Item It; public IconData Icon; }  // Kind: 0 placa, 1 casco, 2 inserto

        ConfigEntry<ShowMode> cMode; ConfigEntry<WarnStyle> cWarnStyle; ConfigEntry<string> cNotifIcon, cSound, cClip, cFile; ConfigEntry<float> cVol; ConfigEntry<KeyCode> cTest;
        ConfigEntry<float> cHideDelay, cHold, cPosX, cPosY, cScale, cWarnSecs, cAlpha, cFade, cIconPct;
        ConfigEntry<bool> cColor;
        ConfigEntry<int> cTextPxI;
        ConfigEntry<string> cFont;
        float fade; Font font; string fontLoaded = null;
        ConfigEntry<KeyCode> cKey;
        ConfigEntry<bool> cGearIcon, cPlateIcons, cRealIcons, cHideUI; bool uiOpenPrev; ConfigEntry<float> cGearPct, cDetail; ConfigEntry<bool> cWarn, cOnlyInRaid, cDebug, cHelmet, cHelmetIns, cOnDamage, cFlash; ConfigEntry<float> cFlashSecs, cFlashStr;
        readonly Dictionary<string, float> flashUntil = new Dictionary<string, float>();
        const float FlashR = 0.95f, FlashG = 0.28f, FlashB = 0.25f; ConfigEntry<float> cThresh; bool belowThresh; ConfigEntry<float> cHX, cHY;
        bool dbgOnce = true;

        readonly List<Row> rows = new List<Row>();
        readonly Dictionary<string, float> last = new Dictionary<string, float>();
        float nextPoll, showUntil, holdStart = -1f, warnUntil;
        bool holdDone;
        Texture2D texShape, texOutline, texGlow; const int GP = 8;
        GUIStyle style; ConfigEntry<float> cGlow, cGlowSize; ConfigEntry<string> cTmpFontName;
        string warnText = "";

        // Atributos que lee Configuration Manager (F12): orden y ocultar entradas
        internal sealed class ConfigurationManagerAttributes { public bool? Browsable; public int? Order; }

        static ConfigDescription D(string desc, AcceptableValueBase range, int order, bool hidden = false)
        {
            return new ConfigDescription(desc, range, new ConfigurationManagerAttributes { Order = order, Browsable = hidden ? (bool?)false : null });
        }
        static ConfigDescription D(string desc, int order, bool hidden = false) { return D(desc, null, order, hidden); }

        // Los mensajes de diagnostico solo salen con "Debug overlay" activado (ajuste oculto en el .cfg)
        void Dbg(string s) { if (cDebug != null && cDebug.Value) Logger.LogInfo(s); }

        void Awake()
        {
            const string G = "1. General", P = "2. Panel", H = "3. Helmet", W = "4. Alerts";
            // ---- 1. General ----
            cMode = Config.Bind(G, "Display mode", ShowMode.AutoHide, D("AutoHide: the panel hides after a few seconds. AlwaysOn: always visible.", 100));
            cKey = Config.Bind(G, "Show key", KeyCode.R, D("Hold this key to show the panel (note: R is also reload).", 90));
            cHold = Config.Bind(G, "Key hold time (s)", 0.5f, D("How long the key must be held to show the panel.", new AcceptableValueRange<float>(0.2f, 2f), 80));
            cHideDelay = Config.Bind(G, "Visible time (s)", 2f, D("Seconds the panel stays visible in AutoHide mode.", new AcceptableValueRange<float>(1f, 30f), 70));
            cOnDamage = Config.Bind(G, "Show on durability loss", true, D("The panel appears every time a plate or helmet insert loses durability.", 60));
            cThresh = Config.Bind(G, "Low durability threshold (%)", 0f, D("The panel stays visible while any component is at or below this percentage. 0 = disabled.", new AcceptableValueRange<float>(0f, 100f), 50));
            cOnlyInRaid = Config.Bind(G, "Raid only", true, D("Hide the panel outside of raids.", 40));
            cDebug = Config.Bind(G, "Debug overlay", false, D("Shows a small debug line on screen and logs the vest slots.", 10, true));
            // ---- 2. Panel ----
            cHideUI = Config.Bind(G, "Hide in menus / inventory", true, D("Fades the panel out whenever the mouse cursor is free (main menu, inventory, pause menu, hideout screens). Turn off to position the panel while F12 is open.", 39));
            cGearIcon = Config.Bind(P, "Show vest / helmet icon", true, D("Shows the silhouette of your equipped vest and helmet (from their inventory icon), filled by their total durability.", 105));
            cPlateIcons = Config.Bind(P, "Show plate / insert icons", true, D("Shows an icon next to each plate and helmet insert. Off: text only.", 104));
            cRealIcons = Config.Bind(P, "Real item icons on plates / inserts", true, D("Uses the game's own icon of each plate, insert and helmet visor (filled by durability) instead of the drawn shapes. Icons are generated once per item type and cached. Off: drawn shapes.", 100));
            cGearPct = Config.Bind(P, "Vest / helmet icon size (%)", 400f, D("Size of the vest and helmet silhouette.", new AcceptableValueRange<float>(80f, 600f), 103));
            cDetail = Config.Bind(P, "Icon detail", 0.7f, D("Blends the real item icon into the silhouette for more texture. 0 = flat silhouette.", new AcceptableValueRange<float>(0f, 1f), 102));
            cPosX = Config.Bind(P, "Position X (%)", 2f, D("0 = left edge, 100 = right edge.", new AcceptableValueRange<float>(0f, 100f), 100));
            cPosY = Config.Bind(P, "Position Y (%)", 29f, D("0 = top edge, 100 = bottom edge.", new AcceptableValueRange<float>(0f, 100f), 90));
            cScale = Config.Bind(P, "Overall scale", 1f, D("Scales the whole panel.", new AcceptableValueRange<float>(0.5f, 3f), 80));
            cIconPct = Config.Bind(P, "Icon size (%)", 100f, D("Size of the plate and helmet icons.", new AcceptableValueRange<float>(40f, 300f), 70));
            cTextPxI = Config.Bind(P, "Text size (px)", 20, D("Size of the text.", new AcceptableValueRange<int>(8, 60), 60));
            cAlpha = Config.Bind(P, "Opacity", 1f, D("Overall opacity of the panel.", new AcceptableValueRange<float>(0.2f, 1f), 50));
            cFade = Config.Bind(P, "Fade duration (s)", 0.6f, D("Fade in / fade out time. 0 = instant.", new AcceptableValueRange<float>(0f, 3f), 40));
            cColor = Config.Bind(P, "Color by wear", false, D("On: the fill color changes with the remaining durability. Off: always white, read the fill level only.", 30));
            cGlow = Config.Bind(P, "Glow strength", 0.5f, D("Soft glow around the text and icons. 0 = off.", new AcceptableValueRange<float>(0f, 3f), 20));
            cGlowSize = Config.Bind(P, "Glow radius (px)", 2f, D("Size of the glow around the text.", new AcceptableValueRange<float>(1f, 8f), 15));
            cFlash = Config.Bind(P, "Damage flash", true, D("The plate or zone that just lost durability briefly turns soft red.", 12));
            cFlashSecs = Config.Bind(P, "Damage flash duration (s)", 3f, D("How long the damage flash lasts.", new AcceptableValueRange<float>(0.5f, 10f), 11));
            cFlashStr = Config.Bind(P, "Damage flash intensity", 0.55f, D("0.1 = very subtle, 1 = strong.", new AcceptableValueRange<float>(0.1f, 1f), 10, true));
            cTmpFontName = Config.Bind(P, "In-game font name", "bender normal", D("Part of the name of the game's TextMeshPro font asset to use. Empty = use the fallback.", 5, true));
            cFont = Config.Bind(P, "Fallback font file", "", D("Only used if the game font is not found. Name or path of a .ttf/.otf file.", 4, true));
            // ---- 3. Helmet ----
            cHelmet = Config.Bind(H, "Show helmet", true, D("Show the overall helmet durability.", 100));
            cHelmetIns = Config.Bind(H, "Show helmet inserts", true, D("Show Top, Nape, Eyes, Jaw and Ears (only the ones the helmet actually has).", 90));
            cHX = Config.Bind(H, "Position X (%)", 1.9f, D("0 = left edge, 100 = right edge.", new AcceptableValueRange<float>(0f, 100f), 80));
            cHY = Config.Bind(H, "Position Y (%)", 50f, D("0 = top edge, 100 = bottom edge.", new AcceptableValueRange<float>(0f, 100f), 70));
            // ---- 4. Alerts ----
            cWarn = Config.Bind(W, "Alert when a component breaks", true, D("Alert when a plate or helmet insert reaches 0 durability.", 100));
            cWarnStyle = Config.Bind(W, "Alert type", WarnStyle.Notification, D("Notification: red Tarkov-style popup. Panel: blinking text under the plates. Both: popup and blinking text.", 90));
            cWarnSecs = Config.Bind(W, "Panel alert duration (s)", 8f, D("How long the blinking text stays (Panel / Both).", new AcceptableValueRange<float>(1f, 20f), 80));
            cVol = Config.Bind(W, "Clip volume", 0.2f, D("Volume of the break sound.", new AcceptableValueRange<float>(0f, 1f), 70));
            cTest = Config.Bind(W, "Test key", KeyCode.None, D("Press it to preview the alert (sound and popup) without breaking a plate. None = disabled.", 60, true));
            cNotifIcon = Config.Bind(W, "Notification icon", "Alert", D("Name of the Tarkov notification icon (see the log: 'Valores de ENotificationIconType').", 50, true));
            cSound = Config.Bind(W, "Alert sound (Tarkov UI)", "ErrorMessage", D("Name of a Tarkov UI sound (see the log: 'Valores de EUISoundType'). Empty = no sound.", 40, true));
            cFile = Config.Bind(W, "Custom sound file", "Broken_Plate_Sound.wav", D("A .wav file (PCM 16/24/32-bit or float) placed next to the plugin DLL (or a full path). Played when a plate / insert breaks. Empty = use the game's UI sound.", 45, true));
            cClip = Config.Bind(W, "Audio clip name", "", D("If set, plays the loaded AudioClip whose name contains this text instead of the UI sound.", 30, true));
            Inst = this;
            try { InvInit(); } catch (Exception e) { Logger.LogWarning("Inventario: " + e.Message); }
            Logger.LogInfo("ShowMeThePlates 1.0.0 loaded");
        }

        void Update()
        {
            Player p = GetPlayer();
            bool uiOpen = cHideUI.Value && (Cursor.visible || Cursor.lockState != CursorLockMode.Locked);
            if (uiOpen != uiOpenPrev) { uiOpenPrev = uiOpen; if (cDebug.Value) Dbg("Cursor libre (menu/inventario): " + uiOpen); }
            float target = (p != null && !uiOpen && Visible()) ? 1f : 0f;
            float step = cFade.Value > 0.01f ? Time.unscaledDeltaTime / cFade.Value : 1f;
            fade = Mathf.MoveTowards(fade, target, step);
            if (p == null) { rows.Clear(); last.Clear(); holdStart = -1f; return; }

            UpdateIcons();
            if (cTest.Value != KeyCode.None && Input.GetKeyDown(cTest.Value)) { PlayWarnSound(); if (cWarnStyle.Value != WarnStyle.Panel) Notify("Front Plate is broken!"); }
            // Pulsacion larga
            if (Input.GetKey(cKey.Value))
            {
                if (holdStart < 0f) { holdStart = Time.unscaledTime; holdDone = false; }
                if (!holdDone && Time.unscaledTime - holdStart >= cHold.Value)
                {
                    holdDone = true; dbgOnce = true; Poll(p); showUntil = Time.unscaledTime + cHideDelay.Value;
                }
            }
            else holdStart = -1f;

            if (Time.unscaledTime >= nextPoll) { nextPoll = Time.unscaledTime + 0.5f; Poll(p); }
        }

        static Player GetPlayer()
        {
            try { var gw = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null; 
                if (gw == null) return null;
                var mp = gw.MainPlayer;
                if (mp == null || !mp.isActiveAndEnabled || !mp.gameObject.activeInHierarchy || mp.Equipment == null) return null;
                return mp; }
            catch { return null; }
        }

        void Poll(Player p)
        {
            try
            {
                var found = new List<Row>(); Item newVest = null, newHelm = null; float vc = 0f, vm = 0f;
                var eq = p.Equipment;
                if (eq == null) return;
                foreach (var es in new[] { EquipmentSlot.TacticalVest, EquipmentSlot.ArmorVest })
                {
                    var slot = eq.GetSlot(es);
                    var vest = slot != null ? slot.ContainedItem : null;
                    if (vest == null) continue;
                    if (newVest == null) newVest = vest;
                    var comp = vest as CompoundItem;
                    if (comp == null) { if (cDebug.Value) Dbg("Chaleco no es CompoundItem: " + vest.GetType().Name); continue; }
                    foreach (var sl in comp.Slots)
                    {
                        string sid = sl.ID ?? "";
                        string low = sid.ToLowerInvariant();
                        if (cDebug.Value && dbgOnce) Dbg("Ranura " + sid + " -> " + (sl.ContainedItem != null ? sl.ContainedItem.TemplateId.ToString() : "(vacia)"));
                        var it0 = sl.ContainedItem;
                        if (it0 != null) { var r0 = it0.GetItemComponent<RepairableComponent>(); if (r0 != null) { vc += r0.Durability; vm += r0.MaxDurability; } }
                        if (!low.Contains("plate") || low.Contains("soft")) continue;
                        var it = sl.ContainedItem;
                        if (it == null) continue;
                        var rep = it.GetItemComponent<RepairableComponent>();
                        if (rep == null) { if (cDebug.Value && dbgOnce) Dbg("Sin Repairable: " + sid); continue; }
                        found.Add(NewRow(it.Id, LabelFor(low), null, 0, 0, rep, it));
                    }
                }
                if (newVest != null && vm > 0f && found.Exists(q => q.Kind == 0)) found.Add(HeaderRow(newVest, 3, vc, vm));
                if (cHelmet.Value)
                {
                    var hs = eq.GetSlot(EquipmentSlot.Headwear);
                    var hi = hs != null ? hs.ContainedItem : null;
                    if (hi != null)
                    {
                        newHelm = hi;
                        var hrep = hi.GetItemComponent<RepairableComponent>();
                        float hcur = hrep != null ? hrep.Durability : 0f, hmax = hrep != null ? hrep.MaxDurability : 0f;
                        var hc = hi as CompoundItem;
                        bool logH = loggedHelmet.Add(hi.Id);
                        if (hc != null)
                            foreach (var sl in hc.Slots)
                            {
                                string low = (sl.ID ?? "").ToLowerInvariant();
                                if (logH) Dbg("Casco ranura " + sl.ID + " -> " + (sl.ContainedItem != null ? sl.ContainedItem.TemplateId.ToString() : "(vacia)"));
                                int z = ZoneFor(low);
                                if (z < 0 && sl.ContainedItem != null && IsVisor(sl, sl.ContainedItem))
                                {
                                    var vrep = sl.ContainedItem.GetItemComponent<RepairableComponent>();
                                    if (vrep != null) { hcur += vrep.Durability; hmax += vrep.MaxDurability; }
                                    if (vrep != null && cHelmetIns.Value) found.Add(NewRow(sl.ContainedItem.Id, "Gear", "Helmet gear", 2, 2, vrep, sl.ContainedItem));
                                    continue;
                                }
                                if (z < 0 || sl.ContainedItem == null) continue;
                                var rep = sl.ContainedItem.GetItemComponent<RepairableComponent>();
                                if (rep == null) continue;
                                hcur += rep.Durability; hmax += rep.MaxDurability;
                                if (cHelmetIns.Value) found.Add(NewRow(sl.ContainedItem.Id, ZoneNames[z], "Helmet " + ZoneNames[z] + " insert", 2, z, rep, sl.ContainedItem));
                            }
                        if (hmax > 0f) found.Add(HeaderRow(hi, 1, hcur, hmax));
                    }
                }
                helmItem = newHelm;
                vestItem = newVest;
                dbgOnce = false;
                found.Sort((a, b) => { int ka = KindOrder(a.Kind), kb = KindOrder(b.Kind); return ka != kb ? ka.CompareTo(kb) : a.Zone != b.Zone ? a.Zone.CompareTo(b.Zone) : PlateRank(a.Label) != PlateRank(b.Label) ? PlateRank(a.Label).CompareTo(PlateRank(b.Label)) : string.CompareOrdinal(a.Label, b.Label); });
                // Aviso al llegar a 0
                foreach (var r in found)
                {
                    float prev;
                    if (last.TryGetValue(r.Id, out prev) && prev - r.Cur > 0.001f)
                    {
                        flashUntil[r.Id] = Time.unscaledTime + cFlashSecs.Value;
                        if (cOnDamage.Value) showUntil = Time.unscaledTime + cHideDelay.Value;
                    }
                    if (last.TryGetValue(r.Id, out prev) && prev > 0.01f && r.Cur <= 0.01f && cWarn.Value && r.Kind != 3)
                    {
                        warnText = (r.Kind == 0 ? r.Label : r.Full) + " is broken!";
                        if (cWarnStyle.Value != WarnStyle.Notification) warnUntil = Time.unscaledTime + cWarnSecs.Value;
                        PlayWarnSound();
                        if (cWarnStyle.Value != WarnStyle.Panel) Notify((r.Kind == 0 ? Nombre(r.Label) : r.Full) + " is broken!");
                        showUntil = Time.unscaledTime + cWarnSecs.Value;
                        Dbg("Placa a 0: " + r.Label);
                    }
                    last[r.Id] = r.Cur;
                }
                bool below = false;
                if (cThresh.Value > 0.01f) foreach (var r in found) if (r.Max > 0f && r.Cur / r.Max * 100f <= cThresh.Value) { below = true; break; }
                belowThresh = below;
                foreach (var r in found) if (r.It != null) { IconData ic; if (icons.TryGetValue(PlateKey(r.It), out ic)) r.Icon = ic; }   // sin parpadeo al refrescar
                rows.Clear(); rows.AddRange(found);
            }
            catch (Exception e) { Logger.LogWarning("Poll: " + e.Message); }
        }

        // Id de la ranura donde esta el item (por reflexion, los tipos de direccion estan ofuscados)
        static string SlotId(Item it)
        {
            try
            {
                var addr = it.CurrentAddress; if (addr == null) return null;
                var cont = addr.GetType().GetProperty("Container", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(addr, null);
                if (cont == null) return null;
                var id = cont.GetType().GetProperty("ID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(cont, null);
                return id as string;
            }
            catch { return null; }
        }

        static readonly string[] ZoneNames = { "Top", "Nape", "Eyes", "Jaw", "Ears" };
        readonly HashSet<string> loggedHelmet = new HashSet<string>();

        static Row NewRow(string id, string label, string full, int kind, int zone, RepairableComponent rep, Item item = null)
        {
            var r = new Row { Id = id, Label = label, Full = full, Kind = kind, Zone = zone, Cur = rep.Durability, Max = rep.MaxDurability, It = item };
            r.Txt = label + " - " + Mathf.Floor(r.Cur + 0.0001f).ToString("0") + "/" + Mathf.Floor(r.Max + 0.0001f).ToString("0");
            return r;
        }

        // Zona del casco segun el id de ranura (Helmet_top, Helmet_back/nape, Helmet_eyes, Helmet_jaw, Helmet_ears)
        static int ZoneFor(string low)
        {
            if (low.StartsWith("mod_")) return -1;
            if (low.Contains("top")) return 0;
            if (low.Contains("nape") || low.Contains("back")) return 1;
            if (low.Contains("eye")) return 2;
            if (low.Contains("jaw")) return 3;
            if (low.Contains("ear")) return 4;
            return -1;
        }

        // Visera / pieza extra blindada del casco (p. ej. cristal del Altyn): ranura "mod_*" cuyo objeto tiene blindaje y durabilidad
        static bool IsVisor(EFT.InventoryLogic.Slot sl, Item ch)
        {
            string low = (sl.ID ?? "").ToLowerInvariant();
            return low.StartsWith("mod_") && ch != null && ch.GetItemComponent<ArmorComponent>() != null && ch.GetItemComponent<RepairableComponent>() != null;
        }

        // Orden de las placas del chaleco: F, R, LS, RS
        static int PlateRank(string label)
        {
            if (label == null) return 9;
            if (label.StartsWith("F.")) return 0;
            if (label.StartsWith("R.")) return 1;
            if (label.StartsWith("LS.")) return 2;
            if (label.StartsWith("RS.")) return 3;
            return 4;
        }

        static int KindOrder(int k) { return k == 3 ? 0 : k == 0 ? 1 : k == 1 ? 2 : 3; }

        Row HeaderRow(Item it, int kind, float cur, float max)
        {
            string nm = GearName(it, kind == 3 ? "Vest" : "Helmet");
            return new Row { Id = it.Id, Label = nm, Full = nm, Kind = kind, Zone = 5, Cur = cur, Max = max, Txt = nm + " - " + Mathf.Floor(cur + 0.0001f).ToString("0") + "/" + Mathf.Floor(max + 0.0001f).ToString("0") };
        }

        // Nombre corto localizado del objeto ("<id> ShortName" en los locales del juego)
        readonly Dictionary<string, string> gearNames = new Dictionary<string, string>();
        MethodInfo locMi; bool locTried;
        string GearName(Item it, string fallback)
        {
            string n;
            if (gearNames.TryGetValue(it.Id, out n)) return n;
            n = null;
            try
            {
                n = Localize(it.TemplateId.ToString() + " ShortName");
                if (string.IsNullOrEmpty(n) || n.EndsWith(" ShortName")) n = Localize(it.TemplateId.ToString() + " Name");
                if (!string.IsNullOrEmpty(n) && n.EndsWith(" Name")) n = null;
            }
            catch { n = null; }
            if (string.IsNullOrEmpty(n)) n = fallback;
            gearNames[it.Id] = n;
            return n;
        }

        string Localize(string key)
        {
            if (!locTried)
            {
                locTried = true;
                Type[] types; try { types = typeof(Player).Assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, x => x != null); }
                foreach (var t in types)
                {
                    if (!(t.IsAbstract && t.IsSealed)) continue;
                    MethodInfo[] ms; try { ms = t.GetMethods(BindingFlags.Public | BindingFlags.Static); } catch { continue; }
                    foreach (var m in ms)
                    {
                        if (m.Name != "Localized" || m.ReturnType != typeof(string)) continue;
                        var ps = m.GetParameters();
                        if (ps.Length >= 1 && ps[0].ParameterType == typeof(string)) { locMi = m; break; }
                    }
                    if (locMi != null) break;
                }
                Dbg(locMi != null ? "Localized: " + locMi.DeclaringType.FullName : "Localized: no encontrado (se usan nombres genericos)");
            }
            if (locMi == null) return null;
            var pp = locMi.GetParameters(); var args = new object[pp.Length];
            args[0] = key;
            for (int i = 1; i < pp.Length; i++) args[i] = pp[i].HasDefaultValue ? pp[i].DefaultValue : (pp[i].ParameterType.IsValueType ? Activator.CreateInstance(pp[i].ParameterType) : null);
            return locMi.Invoke(null, args) as string;
        }

        static string Nombre(string label)
        {
            if (label.StartsWith("F.")) return "Front Plate";
            if (label.StartsWith("R.")) return "Rear Plate";
            if (label.StartsWith("LS.")) return "Left Side Plate";
            if (label.StartsWith("RS.")) return "Right Side Plate";
            return "Plate";
        }

        // Notificacion nativa de Tarkov (misma que usa Boss Notifier), por reflexion para tolerar cambios de firma
        bool clipListed; AudioSource src;
        bool PlayClip()
        {
            string want = (cClip.Value ?? "").Trim().ToLowerInvariant();
            if (!clipListed && want.Length > 0)
            {
                clipListed = true;
                int n = 0; var sb = new System.Text.StringBuilder();
                foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>())
                {
                    string cn = (c.name ?? "").ToLowerInvariant();
                    if (n < 80 && (cn.Contains("alert") || cn.Contains("alarm") || cn.Contains("notif") || cn.Contains("warn") || cn.Contains("error") || cn.Contains("siren") || cn.Contains("beep") || cn.Contains("message"))) { sb.Append(c.name).Append(", "); n++; }
                }
                Dbg("Clips candidatos (" + n + "): " + sb);
            }
            if (want.Length == 0) return false;
            AudioClip hit = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>())
                if ((c.name ?? "").ToLowerInvariant().Contains(want)) { hit = c; break; }
            if (hit == null) { Logger.LogWarning("Clip '" + want + "' no encontrado"); return false; }
            if (src == null) { src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; }
            src.PlayOneShot(hit, cVol.Value);
            return true;
        }

        // ---- Sonido propio (.wav junto a la DLL), decodificado a mano: sin depender de modulos de audio extra ----
        AudioClip fileClip; string fileClipKey;
        bool PlayFile()
        {
            string name = (cFile.Value ?? "").Trim();
            if (name.Length == 0) return false;
            string path = Path.IsPathRooted(name) ? name : Path.Combine(Path.GetDirectoryName(Info.Location), name);
            if (fileClip == null || fileClipKey != path)
            {
                fileClipKey = path; fileClip = null;
                if (!File.Exists(path)) { Logger.LogWarning("Sonido no encontrado: " + path); return false; }
                try { fileClip = LoadWav(path); Dbg("Sonido cargado: " + path); }
                catch (Exception e) { Logger.LogWarning("Sonido .wav no valido: " + e.Message); return false; }
            }
            if (fileClip == null) return false;
            if (src == null) { src = gameObject.AddComponent<AudioSource>(); src.spatialBlend = 0f; src.playOnAwake = false; }
            src.PlayOneShot(fileClip, cVol.Value);
            return true;
        }

        static AudioClip LoadWav(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            if (b.Length < 44 || b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') throw new Exception("no es RIFF/WAV");
            int fmt = 1, ch = 2, rate = 48000, bits = 16, dataPos = -1, dataLen = 0;
            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, pos, 4); int len = BitConverter.ToInt32(b, pos + 4); int body = pos + 8;
                if (id == "fmt ")
                {
                    fmt = BitConverter.ToUInt16(b, body); ch = BitConverter.ToUInt16(b, body + 2); rate = BitConverter.ToInt32(b, body + 4); bits = BitConverter.ToUInt16(b, body + 14);
                    if (fmt == 0xFFFE && len >= 26) fmt = BitConverter.ToUInt16(b, body + 24);   // WAVE_FORMAT_EXTENSIBLE
                }
                else if (id == "data") { dataPos = body; dataLen = Math.Min(len < 0 ? int.MaxValue : len, b.Length - body); break; }
                pos = body + len + (len & 1);
            }
            if (dataPos < 0) throw new Exception("sin bloque data");
            int bps = bits / 8; if (bps < 1 || ch < 1) throw new Exception("formato raro");
            int frames = dataLen / (bps * ch); var f = new float[frames * ch];
            for (int i = 0; i < f.Length; i++)
            {
                int o = dataPos + i * bps; float v;
                if (fmt == 3 && bits == 32) v = BitConverter.ToSingle(b, o);
                else if (bits == 16) v = BitConverter.ToInt16(b, o) / 32768f;
                else if (bits == 24) { int x = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16); if ((x & 0x800000) != 0) x |= unchecked((int)0xFF000000); v = x / 8388608f; }
                else if (bits == 32) v = BitConverter.ToInt32(b, o) / 2147483648f;
                else if (bits == 8) v = (b[o] - 128) / 128f;
                else throw new Exception("bits no soportados: " + bits);
                f[i] = v;
            }
            var clip = AudioClip.Create("ShowMeThePlates_Broken", frames, ch, rate, false);
            clip.SetData(f, 0);
            return clip;
        }

        bool soundTried; object soundInst; MethodInfo soundMi; Type soundEnum;
        void PlayWarnSound()
        {
            try
            {
                if (PlayFile()) return;
                if (PlayClip()) return;
                string want = (cSound.Value ?? "").Trim();
                if (want.Length == 0) return;
                if (!soundTried)
                {
                    soundTried = true;
                    Type gs = typeof(Player).Assembly.GetType("GUISounds");
                    if (gs == null) foreach (var t in typeof(Player).Assembly.GetTypes()) if (t.Name == "GUISounds") { gs = t; break; }
                    if (gs == null) { Logger.LogWarning("Sonido: GUISounds no encontrado"); return; }
                    foreach (var m in gs.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var ps = m.GetParameters();
                        if (m.Name == "PlayUISound" && ps.Length >= 1 && ps[0].ParameterType.IsEnum) { soundMi = m; soundEnum = ps[0].ParameterType; break; }
                    }
                    if (soundMi == null) { Logger.LogWarning("Sonido: PlayUISound no encontrado"); return; }
                    Dbg("Valores de " + soundEnum.Name + ": " + string.Join(", ", Enum.GetNames(soundEnum)));
                    var sing = typeof(Singleton<>).MakeGenericType(gs);
                    soundInst = sing.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                }
                if (soundMi == null) return;
                if (soundInst == null)
                {
                    var sing = typeof(Singleton<>).MakeGenericType(soundMi.DeclaringType);
                    soundInst = sing.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                    if (soundInst == null) return;
                }
                if (!Enum.IsDefined(soundEnum, want)) { Logger.LogWarning("Sonido: '" + want + "' no existe en " + soundEnum.Name); return; }
                var ps2 = soundMi.GetParameters(); var args = new object[ps2.Length];
                args[0] = Enum.Parse(soundEnum, want);
                for (int i = 1; i < ps2.Length; i++) args[i] = ps2[i].HasDefaultValue ? ps2[i].DefaultValue : (ps2[i].ParameterType.IsValueType ? Activator.CreateInstance(ps2[i].ParameterType) : null);
                soundMi.Invoke(soundInst, args);
            }
            catch (Exception e) { Logger.LogWarning("Sonido: " + (e.InnerException ?? e).Message); }
        }

        MethodInfo notifyMi; bool notifyTried;
        void Notify(string text)
        {
            try
            {
                if (notifyMi == null && !notifyTried)
                {
                    notifyTried = true;
                    Type[] types;
                    try { types = typeof(Player).Assembly.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, x => x != null); }
                    var direct = typeof(Player).Assembly.GetType("EFT.Communications.NotificationManager");
                    if (direct != null) foreach (var m in direct.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)) if (m.Name == "DisplayMessageNotification" && (notifyMi == null || m.GetParameters().Length > notifyMi.GetParameters().Length)) notifyMi = m;
                    if (notifyMi != null) foreach (var pp in notifyMi.GetParameters()) { var ue = Nullable.GetUnderlyingType(pp.ParameterType) ?? pp.ParameterType; if (ue.IsEnum) Dbg("Valores de " + ue.Name + ": " + string.Join(", ", Enum.GetNames(ue))); }
                    if (notifyMi != null) { Dbg("Notificacion: EFT.Communications.NotificationManager.DisplayMessageNotification"); types = new Type[0]; }
                    const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;
                    MethodInfo warn = null; int logged = 0;
                    foreach (var t in types)
                    {
                        MethodInfo[] ms;
                        try { ms = t.GetMethods(bf); } catch { continue; }
                        foreach (var m in ms)
                        {
                            if (m.Name == "DisplayMessageNotification" && (notifyMi == null || m.GetParameters().Length > notifyMi.GetParameters().Length)) { notifyMi = m; Dbg("Notificacion: " + t.FullName + "." + m.Name); }
                            else if (m.Name == "DisplayWarningNotification" && warn == null) warn = m;
                            if (m.Name.IndexOf("Notification", StringComparison.OrdinalIgnoreCase) >= 0 && logged < 25) { logged++; Dbg("Notificacion candidato: " + t.FullName + "." + m.Name + "(" + string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name)) + ")"); }
                        }
                    }
                    if (notifyMi == null) notifyMi = warn;
                    if (notifyMi == null) Logger.LogWarning("Notificacion: no se encontro DisplayMessageNotification en " + types.Length + " tipos");
                }
                var mi = notifyMi;
                if (mi == null) return;
                var ps = mi.GetParameters(); var args = new object[ps.Length];
                for (int i = 0; i < ps.Length; i++)
                {
                    Type pt = ps[i].ParameterType; Type ut = Nullable.GetUnderlyingType(pt) ?? pt;
                    if (pt == typeof(string)) args[i] = text;
                    else if (ut.IsEnum)
                    {
                        string[] pref = ut.Name.Contains("Duration") ? new[] { "Long", "Default" } : ut.Name.Contains("Icon") ? new[] { (cNotifIcon.Value ?? "").Trim(), "Default" } : new string[0];
                        object v = null;
                        foreach (var n in pref) if (Enum.IsDefined(ut, n)) { v = Enum.Parse(ut, n); break; }
                        args[i] = v ?? (ps[i].HasDefaultValue ? ps[i].DefaultValue : Activator.CreateInstance(ut));
                    }
                    else if (ut == typeof(Color)) args[i] = Color.white;
                    else args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (pt.IsValueType ? Activator.CreateInstance(pt) : null);
                }
                mi.Invoke(null, args);
            }
            catch (Exception e) { Logger.LogWarning("Notificacion: " + (e.InnerException ?? e).Message); }
        }

        static string LabelFor(string slotLow)
        {
            if (slotLow.Contains("front")) return "F. Plate";
            if (slotLow.Contains("back")) return "R. Plate";
            if (slotLow.Contains("left")) return "LS. Plate";
            if (slotLow.Contains("right")) return "RS. Plate";
            return "Plate";
        }

        bool Visible()
        {
            if (rows.Count == 0) return false;
            if (cMode.Value == ShowMode.AlwaysOn || belowThresh) return true;
            return Time.unscaledTime < showUntil;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            tmpUsed = 0;
            try { DrawHud(); } finally { for (int i = tmpUsed; i < slots.Count; i++) if (slots[i].main != null && slots[i].main.gameObject.activeSelf) { slots[i].main.gameObject.SetActive(false); slots[i].halo.gameObject.SetActive(false); } }
        }

        void DrawFill(Rect ir, Texture2D tex, float vtop, Color col)
        {
            if (vtop <= 0f) return;
            GUI.color = col;
            GUI.DrawTextureWithTexCoords(new Rect(ir.x, ir.yMax - ir.height * vtop, ir.width, ir.height * vtop), tex, new Rect(0f, 0f, 1f, vtop));
        }

        void DrawHud()
        {
            if (cOnlyInRaid.Value && GetPlayer() == null) return;
            Init(); LoadFont(); style.font = font; FindTmpFont();
            if (cDebug.Value && GetPlayer() != null) { style.fontSize = 12; style.normal.textColor = new Color(1f,1f,1f,0.6f); GUI.Label(new Rect(10f, 10f, 500f, 20f), "ShowMeThePlates active - rows: " + rows.Count, style); }
            if (fade <= 0.01f || rows.Count == 0) return;

            float s = cScale.Value;
            float iconH = 22f * s * cIconPct.Value / 100f;
            float gearH = 22f * s * cGearPct.Value / 100f;
            float gap = 8f * s, indent = 18f * s, lw = Mathf.Max(1f, 1.5f * s);
            style.fontSize = Mathf.RoundToInt(cTextPxI.Value * s);
            float xP = Screen.width * cPosX.Value / 100f, yP = Screen.height * cPosY.Value / 100f;
            float xH = Screen.width * cHX.Value / 100f, yH = Screen.height * cHY.Value / 100f;
            float master = cAlpha.Value * Mathf.SmoothStep(0f, 1f, fade);
            bool pulse = Time.unscaledTime < warnUntil && ((int)(Time.unscaledTime * 3f) % 2 == 0);
            float minRow = cTextPxI.Value * s * 1.4f;
            float cyP = -1f, cyH = -1f;

            var old = GUI.color;
            foreach (var r in rows)
            {
                bool grpP = r.Kind == 0 || r.Kind == 3;
                bool header = r.Kind == 3 || r.Kind == 1;
                float frac = r.Max > 0f ? Mathf.Clamp01(r.Cur / r.Max) : 0f;
                Color c = Color.white;
                if (cColor.Value)
                    c = frac <= 0.001f ? new Color(0.9f, 0.15f, 0.15f) : frac < 0.25f ? new Color(0.95f, 0.45f, 0.15f) : frac < 0.5f ? new Color(0.95f, 0.85f, 0.2f) : new Color(0.45f, 0.85f, 0.4f);
                float a = master * ((frac <= 0.001f && pulse) ? 0.35f : 1f);
                float glow = cGlow.Value;
                float fl = 0f, until;
                if (cFlash.Value && flashUntil.TryGetValue(r.Id, out until)) { float rem = until - Time.unscaledTime; if (rem > 0f) fl = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rem / Mathf.Max(0.2f, cFlashSecs.Value))); }
                float x = grpP ? xP : xH, y = grpP ? yP : yH;

                if (header)
                {
                    IconData d = r.Kind == 3 ? vestIcon : helmIcon;
                    bool useIcon = cGearIcon.Value && (d != null || r.Kind == 1);
                    float vh = useIcon ? gearH : 0f, vw = 0f;
                    if (useIcon) vw = d != null ? Mathf.Min(vh * d.W / d.H, vh * 1.8f) : vh;
                    float rowH = Mathf.Max(vh, minRow) + 4f * s;
                    if (useIcon)
                    {
                        var ir = new Rect(x, y + (rowH - vh) / 2f, vw, vh);
                        if (d != null) DrawGearIcon(ir, d, frac, c, a, glow, fl);
                        else DrawAltyn(ir, 1, 0, frac, c, a, glow, fl);
                    }
                    DrawText(new Rect(x + (useIcon ? vw + gap : 0f), y, 500f * s, rowH), r.Txt, a, s);
                    float cy = y + (useIcon ? (rowH + vh) / 2f : rowH * 0.85f);
                    if (grpP) { yP += rowH; cyP = cy; } else { yH += rowH; cyH = cy; }
                    continue;
                }

                bool pIcon = cPlateIcons.Value;
                float rowC = Mathf.Max(pIcon ? iconH : 0f, minRow) + 4f * s;
                float cx = x + indent, mid = y + rowC / 2f;
                float cy0 = grpP ? cyP : cyH;
                if (cy0 >= 0f)
                {
                    float lx = x + indent * 0.3f;
                    GUI.color = new Color(1f, 1f, 1f, 0.3f * a);
                    GUI.DrawTexture(new Rect(lx, cy0, lw, mid - cy0 + lw), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(lx, mid, indent * 0.6f, lw), Texture2D.whiteTexture);
                    if (grpP) cyP = mid; else cyH = mid;
                }
                float iw = 0f;
                if (pIcon)
                {
                    IconData pd = cRealIcons.Value ? r.Icon : null;
                    if (pd != null) { iw = Mathf.Min(iconH * pd.W / pd.H, iconH * 1.8f); DrawGearIcon(new Rect(cx, y + (rowC - iconH) / 2f, iw, iconH), pd, frac, c, a, glow, fl); }
                    else if (r.Kind == 0) { iw = iconH * 0.75f; DrawPlateIcon(new Rect(cx, y + (rowC - iconH) / 2f, iw, iconH), frac, c, a, glow, fl); }
                    else { iw = iconH; DrawAltyn(new Rect(cx, y + (rowC - iconH) / 2f, iw, iconH), 2, Mathf.Clamp(r.Zone, 0, 4), frac, c, a, glow, fl); }
                }
                DrawText(new Rect(cx + iw + (pIcon ? gap : 0f), y, 400f * s, rowC), r.Txt, a, s);
                if (grpP) yP += rowC; else yH += rowC;
            }
            if (Time.unscaledTime < warnUntil)
            {
                float a = master * (pulse ? 0.4f : 1f);
                var wr = new Rect(xP, yP + 4f, 500f * s, minRow + 4f * s);
                DrawText(wr, "! " + warnText, a, s);
            }
            GUI.color = old;
        }

        void DrawPlateIcon(Rect ir, float frac, Color c, float a, float glow, float fl)
        {
            if (glow > 0.01f)
            {
                float px = ir.width / 24f, py = ir.height / 32f;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.55f * glow) * a);
                GUI.DrawTexture(new Rect(ir.x - GP * px, ir.y - GP * py, ir.width + 2f * GP * px, ir.height + 2f * GP * py), texGlow);
            }
            GUI.color = new Color(0f, 0f, 0f, 0.55f * a); GUI.DrawTexture(ir, texShape);
            DrawFill(ir, texShape, frac, new Color(c.r, c.g, c.b, a));
            if (fl > 0f) { GUI.color = new Color(FlashR, FlashG, FlashB, fl * cFlashStr.Value * a); GUI.DrawTexture(ir, texShape); }
        }

        // Icono generico de casco (Altyn). kind 1 = casco completo, 2 = zona de inserto
        void DrawAltyn(Rect ir, int kind, int z, float frac, Color c, float a, float glow, float fl)
        {
            if (glow > 0.01f)
            {
                float px = ir.width / HN;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.55f * glow) * a);
                GUI.DrawTexture(new Rect(ir.x - GPH * px, ir.y - GPH * px, ir.width + 2f * GPH * px, ir.height + 2f * GPH * px), texHelmGlow);
            }
            GUI.color = new Color(1f, 1f, 1f, 0.16f * a); GUI.DrawTexture(ir, texHead);   // cabeza y cuello como guia
            if (kind == 1)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.55f * a); GUI.DrawTexture(ir, texHelm);
                DrawFill(ir, texHelm, (hMin[5] + (hMax[5] - hMin[5] + 1) * frac) / HN, new Color(c.r, c.g, c.b, a));
                if (fl > 0f) { GUI.color = new Color(FlashR, FlashG, FlashB, fl * cFlashStr.Value * a); GUI.DrawTexture(ir, texHelm); }
            }
            else
            {
                GUI.color = new Color(0f, 0f, 0f, 0.3f * a); GUI.DrawTexture(ir, texHelm);
                GUI.color = new Color(0f, 0f, 0f, 0.6f * a); GUI.DrawTexture(ir, texZone[z]);
                DrawFill(ir, texZone[z], (hMin[z] + (hMax[z] - hMin[z] + 1) * frac) / HN, new Color(c.r, c.g, c.b, a));
                if (fl > 0f) { GUI.color = new Color(FlashR, FlashG, FlashB, fl * cFlashStr.Value * a); GUI.DrawTexture(ir, texZone[z]); }
            }
        }

        // Silueta del objeto (a partir de su icono) rellena por durabilidad, con halo y detalle
        void DrawGearIcon(Rect ir, IconData d, float frac, Color c, float a, float glow, float fl)
        {
            if (glow > 0.01f)
            {
                float px = ir.width / d.W, py = ir.height / d.H;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(0.55f * glow) * a);
                GUI.DrawTexture(new Rect(ir.x - IGP * px, ir.y - IGP * py, ir.width + 2f * IGP * px, ir.height + 2f * IGP * py), d.Glow);
            }
            GUI.color = new Color(0f, 0f, 0f, 0.55f * a); GUI.DrawTexture(ir, d.Mask);
            DrawFill(ir, d.Mask, frac, new Color(c.r, c.g, c.b, a));
            float det = cDetail.Value;
            if (det > 0.01f) { GUI.color = new Color(1f, 1f, 1f, det * a); GUI.DrawTexture(ir, d.Lum); }
            if (fl > 0f) { GUI.color = new Color(FlashR, FlashG, FlashB, fl * cFlashStr.Value * a); GUI.DrawTexture(ir, d.Mask); }
        }

        // ---- Siluetas a partir del icono del objeto ----
        class IconData { public Texture2D Mask, Lum, Glow; public int W, H; }
        const int IGP = 12;
        readonly Dictionary<string, IconData> icons = new Dictionary<string, IconData>();
        readonly Dictionary<string, float> iconNext = new Dictionary<string, float>();
        readonly Dictionary<string, int> iconTries = new Dictionary<string, int>();
        readonly Dictionary<string, Task> iconPending = new Dictionary<string, Task>();
        UnityEngine.Object scanned; Item vestItem, helmItem; IconData vestIcon, helmIcon;
        List<MethodInfo> iconCands; MethodInfo iconWorking;

        void UpdateIcons()
        {
            if (cPlateIcons.Value && cRealIcons.Value)
                for (int i = 0; i < rows.Count; i++) { var r = rows[i]; if (r.Icon == null && r.It != null && (r.Kind == 0 || r.Kind == 2)) r.Icon = GetIcon(r.It, PlateKey(r.It)); }
            if (!cGearIcon.Value) return;
            vestIcon = GetIcon(vestItem);
            helmIcon = GetIcon(helmItem);
        }

        static object ResultOf(Task t)
        {
            var pr = t.GetType().GetProperty("Result");
            return pr != null ? pr.GetValue(t, null) : null;
        }

        static string PlateKey(Item it) { return "t:" + it.TemplateId; }   // placas/insertos sin mods: el icono es igual para todo el mismo tipo

        IconData GetIcon(Item it, string key = null)
        {
            if (it == null) return null;
            string k = key ?? it.Id;
            IconData d;
            if (icons.TryGetValue(k, out d)) return d;
            try
            {
                Task pt;
                if (iconPending.TryGetValue(k, out pt))
                {
                    if (!pt.IsCompleted) return null;
                    iconPending.Remove(k);
                    object res = ResultOf(pt);
                    if (res != null) { d = MakeIcon(res, it); if (d != null) { icons[k] = d; return d; } }
                    return null;
                }
                float nx;
                if (iconNext.TryGetValue(k, out nx) && Time.unscaledTime < nx) return null;
                iconNext[k] = Time.unscaledTime + 3f;
                int tries; iconTries.TryGetValue(k, out tries); iconTries[k] = ++tries;
                if (tries > 4) { icons[k] = null; Logger.LogWarning("Icono: sin icono para " + it.TemplateId); return null; }
                if (iconCands == null) DiscoverIconMethods(it);
                var order = new List<MethodInfo>();
                if (iconWorking != null) order.Add(iconWorking);
                foreach (var m in iconCands) if (m != iconWorking) order.Add(m);
                foreach (var mi in order)
                {
                    if (!mi.IsStatic) continue;
                    try
                    {
                        var ps = mi.GetParameters(); var args = new object[ps.Length];
                        args[0] = it;
                        for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
                        for (int i = 1; i < ps.Length; i++) if (ps[i].ParameterType == typeof(bool) && ps[i].Name != null && ps[i].Name.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0) args[i] = false;   // no guardar el icono a disco (falla con texturas no legibles)
                        object r = mi.Invoke(null, args);
                        var tk = r as Task;
                        if (tk != null)
                        {
                            if (!tk.IsCompleted) { iconPending[k] = tk; iconWorking = mi; return null; }
                            r = ResultOf(tk);
                        }
                        if (r != null) { d = MakeIcon(r, it); if (d != null) { iconWorking = mi; icons[k] = d; Dbg("Icono obtenido con " + mi.DeclaringType.FullName + "." + mi.Name + " para " + it.TemplateId); return d; } }
                    }
                    catch (Exception e) { Dbg("Icono: " + mi.Name + " fallo: " + (e.InnerException ?? e).Message); }
                }
                if (scanned != null) { d = MakeIcon(scanned, it); if (d != null) { icons[k] = d; Dbg("Icono obtenido por busqueda de sprite para " + it.TemplateId); return d; } }
            }
            catch (Exception e) { Logger.LogWarning("Icono: " + e.Message); }
            return null;
        }

        void DiscoverIconMethods(Item item)
        {
            iconCands = new List<MethodInfo>();
            Type[] types;
            try { types = typeof(Player).Assembly.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, x => x != null); }
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var first = new List<MethodInfo>(); var rest = new List<MethodInfo>();
            foreach (var t in types)
            {
                MethodInfo[] ms; try { ms = t.GetMethods(bf); } catch { continue; }
                foreach (var m in ms)
                {
                    Type rt = m.ReturnType; Type inner = rt.IsGenericType ? rt.GetGenericArguments()[0] : rt;
                    if (inner != typeof(Sprite) && inner != typeof(Texture2D) && inner != typeof(Texture) && inner != typeof(RenderTexture)) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 0 || !ps[0].ParameterType.IsAssignableFrom(item.GetType())) continue;
                    if (m.Name.IndexOf("Mod", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // iconos de ranura, no del objeto
                    (m.Name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 || m.Name.IndexOf("Sprite", StringComparison.OrdinalIgnoreCase) >= 0 ? first : rest).Add(m);
                }
            }
            iconCands.AddRange(first); iconCands.AddRange(rest);
            if (cDebug.Value) foreach (var m in iconCands) Dbg("Icono candidato: " + m.DeclaringType.FullName + "." + m.Name + "(" + string.Join(", ", Array.ConvertAll(m.GetParameters(), p => p.ParameterType.Name)) + ") -> " + m.ReturnType.Name + (m.IsStatic ? " [static]" : " [instancia]"));
            try
            {
                string tid = item.TemplateId.ToString(), iid = item.Id;
                foreach (var sp in Resources.FindObjectsOfTypeAll<Sprite>())
                    if (sp != null && sp.name != null && (sp.name.Contains(tid) || sp.name.Contains(iid))) { scanned = sp; break; }
            }
            catch { }
        }

        IconData MakeIcon(object o, Item it)
        {
            Texture2D src = null; Rect rect = default(Rect);
            var sp = o as Sprite;
            if (sp != null) { src = sp.texture; rect = sp.textureRect; }
            else { src = o as Texture2D; if (src != null) rect = new Rect(0, 0, src.width, src.height); }
            var rtx = o as RenderTexture;
            if (src == null && rtx != null)
            {
                var prev0 = RenderTexture.active; RenderTexture.active = rtx;
                var rd0 = new Texture2D(rtx.width, rtx.height, TextureFormat.RGBA32, false);
                rd0.ReadPixels(new Rect(0, 0, rtx.width, rtx.height), 0, 0); rd0.Apply(); RenderTexture.active = prev0;
                src = rd0; rect = new Rect(0, 0, rd0.width, rd0.height);
            }
            if (src == null) return null;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            int w = Mathf.Max(1, (int)rect.width), h = Mathf.Max(1, (int)rect.height);
            var rd = new Texture2D(w, h, TextureFormat.RGBA32, false);
            rd.ReadPixels(new Rect(rect.x, rect.y, w, h), 0, 0); rd.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var px = rd.GetPixels32();
            int x0 = w, x1 = -1, y0 = h, y1 = -1;
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++) if (px[j * w + i].a > 90) { if (i < x0) x0 = i; if (i > x1) x1 = i; if (j < y0) y0 = j; if (j > y1) y1 = j; }
            if (x1 < 0) return null;
            int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
            var alpha = new float[cw * ch]; var lum = new float[cw * ch];
            double sum = 0; int cnt = 0;
            for (int j = 0; j < ch; j++) for (int i = 0; i < cw; i++)
            {
                var q = px[(j + y0) * w + i + x0];
                float al = q.a > 90 ? Mathf.Min(1f, q.a / 127f) : 0f;
                alpha[j * cw + i] = al;
                float l = (0.299f * q.r + 0.587f * q.g + 0.114f * q.b) / 255f;
                lum[j * cw + i] = l;
                if (al > 0f) { sum += l; cnt++; }
            }
            float mean = cnt > 0 ? (float)(sum / cnt) : 0.5f;
            var mask = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            var lumT = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            var mp = new Color32[cw * ch]; var lp = new Color32[cw * ch];
            for (int k = 0; k < cw * ch; k++)
            {
                byte ab = (byte)Mathf.RoundToInt(alpha[k] * 255f);
                mp[k] = new Color32(255, 255, 255, ab);
                float v = Mathf.Clamp01(0.8f + (lum[k] - mean) * 1.4f);
                byte vb = (byte)Mathf.RoundToInt(v * 255f);
                lp[k] = new Color32(vb, vb, vb, ab);
            }
            mask.SetPixels32(mp); mask.Apply(); mask.filterMode = FilterMode.Bilinear; mask.wrapMode = TextureWrapMode.Clamp;
            lumT.SetPixels32(lp); lumT.Apply(); lumT.filterMode = FilterMode.Bilinear; lumT.wrapMode = TextureWrapMode.Clamp;
            // Halo suave con padding
            int GW = cw + 2 * IGP, GH = ch + 2 * IGP;
            var m1 = new float[GW * GH]; var m2 = new float[GW * GH];
            for (int j = 0; j < ch; j++) for (int i = 0; i < cw; i++) m1[(j + IGP) * GW + i + IGP] = alpha[j * cw + i];
            for (int pass = 0; pass < 3; pass++)
            {
                for (int j = 0; j < GH; j++) for (int i = 0; i < GW; i++)
                { float sm = 0f; for (int k = -2; k <= 2; k++) { int ii = i + k; if (ii >= 0 && ii < GW) sm += m1[j * GW + ii]; } m2[j * GW + i] = sm / 5f; }
                for (int j = 0; j < GH; j++) for (int i = 0; i < GW; i++)
                { float sm = 0f; for (int k = -2; k <= 2; k++) { int jj = j + k; if (jj >= 0 && jj < GH) sm += m2[jj * GW + i]; } m1[j * GW + i] = sm / 5f; }
            }
            var gt = new Texture2D(GW, GH, TextureFormat.RGBA32, false);
            var gp = new Color32[GW * GH];
            for (int k = 0; k < GW * GH; k++) gp[k] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(m1[k] * 1.6f) * 255f));
            gt.SetPixels32(gp); gt.Apply(); gt.filterMode = FilterMode.Bilinear; gt.wrapMode = TextureWrapMode.Clamp;
            if (cDebug.Value)
            {
                try
                {
                    string dir = Path.GetDirectoryName(Info.Location);
                    File.WriteAllBytes(Path.Combine(dir, "icon_" + it.TemplateId + "_original.png"), rd.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(dir, "icon_" + it.TemplateId + "_mask.png"), mask.EncodeToPNG());
                }
                catch { }
            }
            return new IconData { Mask = mask, Lum = lumT, Glow = gt, W = cw, H = ch };
        }

        // ---- Icono de casco (perfil tipo Altyn) y sus zonas ----
        // Mapa 96x96: '.' vacio, 0 Top, 1 Nape, 2 Eyes, 3 Jaw, 4 Ears, 5 resto del casco, 6 cabeza/cuello (guia)
        const int HN = 96, GPH = 16;
        Texture2D texHelm, texHelmGlow, texHead; Texture2D[] texZone = new Texture2D[5];
        readonly int[] hMin = new int[6], hMax = new int[6];   // [0..4] zonas, [5] silueta completa

        static readonly string[] HMAP =
        {
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................................................................................",
            "................................000000000000....................................................",
            "............................00000000000000000000................................................",
            "..........................000000000000000000000000..............................................",
            "........................00000000000000000000000000000...........................................",
            "......................0000000000000000000000000000000...........................................",
            "....................000000000000000000000000000000000...00......................................",
            "...................0000000000000000000000000000000000...000000..................................",
            "..................00000000000000000000000000000000000...0000000000..............................",
            ".................000000000000000000000000000000000000...0000000000000...........................",
            "................0000000000000000000000000000000000000..00000000000000...........................",
            "...............00000000000000000000000000000000000000..000000000000000..........................",
            "..............000000000000000000000000000000000000000..0000000000000000.........................",
            ".............0000000000000000000000000000000000000000..0000000000000000.........................",
            ".............0000000000000000000000000000000000000000..00000000000000000........................",
            "............00000000000000000000000000000000000000000..000000000000000000.......................",
            "............0000000000000000000000000000000000000000...000000000000000000.......................",
            "...........00000000000000000000000000000000000000000...0000000000000000000......................",
            "...........00000000000000000000000000000000000000000...00000000000000000000.....................",
            "..........000000000000000000000000000000000000000000...00000000000000000000.....................",
            "..........000000000000000000000000000000000000000000..0000000000000000000000....................",
            "..........000000000000000000000000000000000000000000..00000000000000000000000...................",
            "..........000000000000000000000000000000000000000000..00000000000000000000000...................",
            ".........0000000000000000000000000000000000000000000..00000000000000000000222222222222222222....",
            ".........1111111111111110000000000000000000000000000..000000000000000000022222222222222222222...",
            ".........1111111111111110000000000000000000000000000..0000000000000000002222222222222222222222..",
            ".........1111111111111110000000000000000000000000000..0000000000000000002222222222222222222222..",
            ".........1111111111111110000000000000000000000000000..0000000000000000002222222222222222222222..",
            ".........1111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            "...1111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            "..11111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".111111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".111111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".111111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".111111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".111111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".111111111111111111111111444444444444444444444444444..5555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444.5555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444.5555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444.5555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444.55555....5555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..5555....5555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..5555....5555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..5555....5555555552222222222222222222222..",
            "..1111111111111111111111.4444444444444444444444444444..555555555555555552222222222222222222222..",
            ".......................................................555555555555555552222222222222222222222..",
            "...11111111111111111111114444444444444444444444444444..555555555555555552222222222222222222222..",
            "..111111111111111111111114444444444444444444444444444..555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..555555555555555552222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..333333333333333332222222222222222222222..",
            ".1111111111111111111111114444444444444444444444444444..333333333333333332222222222222222222222..",
            "..111111111111111111111114444444444444444444444444444..33333333333333333322222222222222222222...",
            "...11111111111111111111114444444444444444444444444444..3333333333333333333222222222222222222....",
            "...............................5555555555555666666666..333333333333333333333333333..............",
            "................................6666666666666666666666.333333333333333333333333333..............",
            "................................6666666666666666666666..33333333333333333333333333..............",
            "................................6666666666666666666666..33333333333333333333333333..............",
            "................................6666666666666666666666..33333333333333333333333333..............",
            "................................6666666666666666666666..33333333333333333333333333..............",
            "................................6666666666666666666666..33333333333333333333333333..............",
            "................................6666666666666666666666..33333333333333333333333333..............",
            "................................66666666666666666666666.33333333333333333333333333..............",
            "................................66666666666666666666666..333333333333333333333333...............",
            "................................66666666666666666666666..333333333333333333333333...............",
            "................................66666666666666666666666..333333333333333333333333...............",
            "...............................666666666666666666666666..33333333333333333333333................",
            "...............................666666666666666666666666..33333333333333333333333................",
            "...............................666666666666666666666666..33333333333333333333333................",
            "...............................666666666666666666666666...3333333333333333333333................",
            "...............................6666666666666666666666666..333333333333333333333.................",
            "...............................6666666666666666666666666..333333333333333333333.................",
            "...............................6666666666666666666666666...33333333333333333333.................",
            "...............................6666666666666666666666666....333333333333333333..................",
            "...............................66666666666666666666666666.....333333333333333...................",
            "...............................66666666666666666666666666.......33333333333.....................",
            "...............................66666666666666666666666666........3333333........................",
            "...............................66666666666666666666666666..........333..........................",
            "...............................66666666666666666666666666.......................................",
            "..............................666666666666666666666666666.......................................",
            "..............................6666666666666666666666666666......................................",
            "..............................6666666666666666666666666666......................................",
            "..............................6666666666666666666666666666......................................",
            "..............................6666666666666666666666666666......................................",
            "..............................6666666666666666666666666666......................................",
            "..............................6666666666666666666666666666......................................",
            "..............................6666666666666666666666666666......................................",
            "................................................................................................"
        };

        static int HCls(int i, int j)   // j hacia arriba
        {
            char c = HMAP[HN - 1 - j][i];
            return c == '.' ? -1 : c - '0';
        }

        void BuildHelmet()
        {
            var zmap = new int[HN * HN];
            for (int k = 0; k < 6; k++) { hMin[k] = HN; hMax[k] = -1; }
            for (int j = 0; j < HN; j++) for (int i = 0; i < HN; i++)
            {
                int z = HCls(i, j); zmap[j * HN + i] = z;
                if (z >= 0 && z <= 5)
                {
                    hMin[5] = Mathf.Min(hMin[5], j); hMax[5] = Mathf.Max(hMax[5], j);
                    if (z <= 4) { hMin[z] = Mathf.Min(hMin[z], j); hMax[z] = Mathf.Max(hMax[z], j); }
                }
            }
            for (int k = 0; k < 5; k++) if (hMax[k] < 0) { hMin[k] = 0; hMax[k] = 0; }
            texHelm = MaskTex(zmap, -2);
            texHead = MaskTex(zmap, 6);
            for (int k = 0; k < 5; k++) texZone[k] = MaskTex(zmap, k);
            // Halo suave de la silueta del casco
            int GW = HN + 2 * GPH;
            var m = new float[GW * GW]; var m2 = new float[GW * GW];
            for (int j = 0; j < HN; j++) for (int i = 0; i < HN; i++) { int z = zmap[j * HN + i]; if (z >= 0 && z <= 5) m[(j + GPH) * GW + i + GPH] = 1f; }
            for (int pass = 0; pass < 3; pass++)
            {
                for (int j = 0; j < GW; j++) for (int i = 0; i < GW; i++)
                { float sum = 0f; for (int k = -3; k <= 3; k++) { int ii = i + k; if (ii >= 0 && ii < GW) sum += m[j * GW + ii]; } m2[j * GW + i] = sum / 7f; }
                for (int j = 0; j < GW; j++) for (int i = 0; i < GW; i++)
                { float sum = 0f; for (int k = -3; k <= 3; k++) { int jj = j + k; if (jj >= 0 && jj < GW) sum += m2[jj * GW + i]; } m[j * GW + i] = sum / 7f; }
            }
            var gt = new Texture2D(GW, GW, TextureFormat.RGBA32, false);
            for (int j = 0; j < GW; j++) for (int i = 0; i < GW; i++) gt.SetPixel(i, j, new Color(1f, 1f, 1f, Mathf.Clamp01(m[j * GW + i] * 1.5f)));
            gt.Apply(); gt.filterMode = FilterMode.Bilinear; gt.wrapMode = TextureWrapMode.Clamp; texHelmGlow = gt;
        }

        // zone = -2: silueta del casco (0..5); >=0: solo esa clase
        static Texture2D MaskTex(int[] zmap, int zone)
        {
            var t = new Texture2D(HN, HN, TextureFormat.RGBA32, false);
            for (int j = 0; j < HN; j++) for (int i = 0; i < HN; i++)
            {
                int z = zmap[j * HN + i];
                bool on = zone == -2 ? (z >= 0 && z <= 5) : z == zone;
                t.SetPixel(i, j, on ? Color.white : Color.clear);
            }
            t.Apply(); t.filterMode = FilterMode.Bilinear; t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ---- Texto con TextMeshPro (fuente Bender del juego) ----
        TMP_FontAsset tmpFont; float nextTmpTry;
        Canvas canvas;
        int tmpUsed;

        int tmpTries;
        void FindTmpFont()
        {
            if (tmpFont != null || tmpTries >= 30 || Time.unscaledTime < nextTmpTry) return;
            nextTmpTry = Time.unscaledTime + 10f; tmpTries++;
            string want = (cTmpFontName.Value ?? "").Trim().ToLowerInvariant();
            if (want.Length == 0) { tmpTries = 99; return; }
            TMP_FontAsset best = null;
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                string n = (f.name ?? "").ToLowerInvariant();
                if (n.Contains(want) && !n.Contains("don't use") && (best == null || n.Length < best.name.Length)) best = f;
            }
            if (best != null) { tmpFont = best; Dbg("Fuente TMP elegida: " + best.name); }
            else if (tmpTries == 30) Logger.LogWarning("Fuente TMP '" + want + "' no encontrada; se usa IMGUI");
        }

        void EnsureCanvas()
        {
            if (canvas != null) return;
            var go = new GameObject("PlateHudCanvas");
            DontDestroyOnLoad(go);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
        }

        class Slot { public TextMeshProUGUI main, halo; public string txt; public int size = -1; public Vector2 pos, sz; public float a = -1f, ha = -1f, dil = -9f; public Material hm; }
        readonly List<Slot> slots = new List<Slot>();

        TextMeshProUGUI MakeTmp(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas.transform, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = tmpFont;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return t;
        }

        Slot GetSlot(int i)
        {
            while (slots.Count <= i)
            {
                var sl = new Slot();
                sl.halo = MakeTmp("PlateHudHalo" + slots.Count);   // primero: queda detras
                sl.main = MakeTmp("PlateHudText" + slots.Count);
                sl.hm = sl.halo.fontMaterial;
                slots.Add(sl);
            }
            return slots[i];
        }

        static void Fill(TextMeshProUGUI t, Slot sl, string txt, int size, Vector2 pos, Vector2 sz)
        {
            if (sl.txt != txt) t.text = txt;
            if (t.fontSize != size) t.fontSize = size;
            var rt = t.rectTransform;
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
            if (rt.sizeDelta != sz) rt.sizeDelta = sz;
        }

        void DrawText(Rect r, string txt, float a, float s)
        {
            if (tmpFont == null) { DrawGlow(r, txt, a, s); return; }
            try
            {
                EnsureCanvas();
                var sl = GetSlot(tmpUsed++);
                if (!sl.main.gameObject.activeSelf) { sl.main.gameObject.SetActive(true); sl.halo.gameObject.SetActive(true); }
                int size = Mathf.RoundToInt(cTextPxI.Value * s);
                var pos = new Vector2(r.x, -r.y); var sz = new Vector2(r.width, r.height);
                float g = cGlow.Value;
                float dil = Mathf.Clamp(0.08f + 0.05f * cGlowSize.Value, 0.05f, 0.6f);
                Fill(sl.main, sl, txt, size, pos, sz);
                Fill(sl.halo, sl, txt, size, pos, sz);
                sl.txt = txt;
                if (Mathf.Abs(sl.a - a) > 0.002f) { sl.a = a; sl.main.color = new Color(1f, 1f, 1f, a); }
                float ha = Mathf.Clamp01(0.4f * g) * a;
                if (g <= 0.01f) { if (sl.halo.enabled) sl.halo.enabled = false; }
                else
                {
                    if (!sl.halo.enabled) sl.halo.enabled = true;
                    if (Mathf.Abs(sl.ha - ha) > 0.002f) { sl.ha = ha; sl.halo.color = new Color(1f, 1f, 1f, ha); }
                    if (Mathf.Abs(sl.dil - dil) > 0.001f && sl.hm != null)
                    {
                        sl.dil = dil;
                        if (sl.hm.HasProperty("_FaceDilate")) sl.hm.SetFloat("_FaceDilate", dil);
                        if (sl.hm.HasProperty("_OutlineSoftness")) sl.hm.SetFloat("_OutlineSoftness", 0.5f);
                    }
                }
            }
            catch (Exception e) { if (!tmpErr) { tmpErr = true; Logger.LogWarning("TMP: " + e.Message); } tmpFont = null; tmpTries = 99; DrawGlow(r, txt, a, s); }
        }
        bool tmpErr;

        void DrawGlow(Rect r, string t, float a, float s)
        {
            float g = cGlow.Value;
            if (g > 0.01f)
            {
                float rad = Mathf.Max(1f, cGlowSize.Value * s);
                style.normal.textColor = new Color(1f, 1f, 1f, 0.07f * g * a);
                for (int i = 0; i < 12; i++)
                {
                    float ang = i * Mathf.PI * 2f / 12f;
                    GUI.Label(new Rect(r.x + Mathf.Cos(ang) * rad, r.y + Mathf.Sin(ang) * rad, r.width, r.height), t, style);
                    GUI.Label(new Rect(r.x + Mathf.Cos(ang) * rad * 0.5f, r.y + Mathf.Sin(ang) * rad * 0.5f, r.width, r.height), t, style);
                }
            }
            style.normal.textColor = new Color(0f, 0f, 0f, 0.6f * a);
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), t, style);
            style.normal.textColor = new Color(1f, 1f, 1f, a);
            GUI.Label(r, t, style);
        }

        float nextFontTry;
        void LoadFont()
        {
            string want = (cFont.Value ?? "").Trim();
            if (want != fontLoaded) { fontLoaded = want; font = null; nextFontTry = 0f; }
            if (font != null || want.Length == 0) return;
            if (Time.unscaledTime < nextFontTry) return;
            nextFontTry = Time.unscaledTime + 5f;
            try
            {
                string p = Path.IsPathRooted(want) ? want : Path.Combine(Path.GetDirectoryName(Info.Location), want);
                if (File.Exists(p)) { font = new Font(p); Dbg("Fuente cargada: " + p); }
                else Logger.LogWarning("Fuente no encontrada: " + p);
            }
            catch (Exception e) { Logger.LogWarning("Fuente: " + e.Message); }
        }

        void Init()
        {
            if (texShape != null) return;
            const int W = 24, H = 32;
            var shape = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var outl = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    bool inside = Inside(i, j, W, H), edge = inside && (!Inside(i - 1, j, W, H) || !Inside(i + 1, j, W, H) || !Inside(i, j - 1, W, H) || !Inside(i, j + 1, W, H));
                    shape.SetPixel(i, j, inside ? Color.white : Color.clear);
                    outl.SetPixel(i, j, edge ? Color.white : Color.clear);
                }
            shape.Apply(); outl.Apply(); shape.filterMode = outl.filterMode = FilterMode.Bilinear;
            texShape = shape; texOutline = outl; BuildHelmet();
            // Halo suave: mascara de la forma con padding, desenfocada
            int GW = W + 2 * GP, GH = H + 2 * GP;
            var m = new float[GW * GH]; var m2 = new float[GW * GH];
            for (int j = 0; j < H; j++) for (int i = 0; i < W; i++) if (Inside(i, j, W, H)) m[(j + GP) * GW + i + GP] = 1f;
            for (int pass = 0; pass < 3; pass++)
            {
                for (int j = 0; j < GH; j++) for (int i = 0; i < GW; i++)
                { float sum = 0f; for (int k = -2; k <= 2; k++) { int ii = i + k; if (ii >= 0 && ii < GW) sum += m[j * GW + ii]; } m2[j * GW + i] = sum / 5f; }
                for (int j = 0; j < GH; j++) for (int i = 0; i < GW; i++)
                { float sum = 0f; for (int k = -2; k <= 2; k++) { int jj = j + k; if (jj >= 0 && jj < GH) sum += m2[jj * GW + i]; } m[j * GW + i] = sum / 5f; }
            }
            var gt = new Texture2D(GW, GH, TextureFormat.RGBA32, false);
            for (int j = 0; j < GH; j++) for (int i = 0; i < GW; i++) gt.SetPixel(i, j, new Color(1f, 1f, 1f, Mathf.Clamp01(m[j * GW + i] * 1.6f)));
            gt.Apply(); gt.filterMode = FilterMode.Bilinear; gt.wrapMode = TextureWrapMode.Clamp; texGlow = gt;
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, wordWrap = false };
        }

        // Forma de placa balistica: rectangulo con esquinas superiores recortadas y base redondeada
        static bool Inside(int i, int j, int W, int H)
        {
            if (i < 1 || i >= W - 1 || j < 1 || j >= H - 1) return false;
            int jj = H - 1 - j;                       // j=0 abajo en textura
            int cut = 7;
            if (jj < cut && (i < cut - jj || i > W - 1 - (cut - jj))) return false; // esquinas recortadas (arriba)
            if (j < 6) { int r = 6 - j; if (i < r || i > W - 1 - r) return false; }  // base redondeada
            return true;
        }
    }
}
